import { createHttpTransport, type Scheduler } from "../../src/http-transport.ts";
import { routes } from "../../src/http-routes.ts";
import type { CreateWorkItem } from "../../src/work/items/create.ts";
import type { LiveFailure, LiveState, QueryDescriptor, Subscription, Transport } from "../../src/transport.ts";

export type Identity = "editor" | "viewer" | "anonymous";

const credentials: Record<Identity, string | undefined> = {
    editor: "Basic " + btoa("editor:secret"),
    viewer: "Basic " + btoa("viewer:secret"),
    anonymous: undefined
};

function argument(name: string): string {
    const index = process.argv.indexOf("--" + name);
    const value = index < 0 ? undefined : process.argv[index + 1];
    if (value === undefined) throw new Error("Missing argument --" + name);
    return value;
}

/** A scheduler that holds every delay of `holdFromMs` or more (reconnect backoff) until released, and runs shorter ones (the idle watchdog) on real timers. */
export interface HeldScheduler extends Scheduler {
    held(): number;
    release(): void;
}

export function heldScheduler(holdFromMs = 1000): HeldScheduler {
    const pending: { callback: () => void; cancelled: boolean }[] = [];
    return {
        schedule(callback, delayMs) {
            if (delayMs < holdFromMs) {
                const handle = setTimeout(callback, delayMs);
                return () => clearTimeout(handle);
            }

            const entry = { callback, cancelled: false };
            pending.push(entry);
            return () => { entry.cancelled = true; };
        },
        held: () => pending.filter(entry => !entry.cancelled).length,
        release() {
            const due = pending.splice(0).filter(entry => !entry.cancelled);
            for (const entry of due) entry.callback();
        }
    };
}

export interface Connection {
    readonly transport: Transport;
    readonly identity: () => Identity;
    switchTo(next: Identity): void;
}

export interface ConnectionOptions {
    readonly identity?: Identity;
    readonly scheduler?: Scheduler;
    readonly headers?: Readonly<Record<string, string>>;
}

export function connect(options: ConnectionOptions = {}): Connection {
    let identity: Identity = options.identity ?? "editor";
    const extra = options.headers ?? {};
    const fetchWithHeaders: typeof globalThis.fetch = (input, init) => {
        const headers = new Headers(init?.headers);
        for (const [name, value] of Object.entries(extra)) headers.set(name, value);
        return globalThis.fetch(input, { ...init, headers });
    };
    const transport = createHttpTransport(routes, {
        baseUrl: argument("base"),
        authorization: () => credentials[identity],
        heartbeatMs: Number(argument("heartbeat-ms")),
        fetch: fetchWithHeaders,
        ...(options.scheduler === undefined ? {} : { scheduler: options.scheduler })
    });
    return {
        transport,
        identity: () => identity,
        switchTo(next) {
            identity = next;
            transport.changeIdentity();
        }
    };
}

export type Seen<R> =
    | { readonly kind: "value"; readonly value: R; readonly epoch: number }
    | { readonly kind: "state"; readonly state: LiveState; readonly failure: LiveFailure | undefined }
    | { readonly kind: "cleared" };

export interface Watched<R> {
    readonly events: readonly Seen<R>[];
    readonly subscription: Subscription;
    values(): R[];
    states(): LiveState[];
    failures(): LiveFailure[];
}

export function observe<K extends object, R>(transport: Transport, query: QueryDescriptor<K, R>, key: K): Watched<R> {
    const events: Seen<R>[] = [];
    const subscription = transport.watch(query, key, {
        value: (value, epoch) => { events.push({ kind: "value", value, epoch }); },
        state: (state, _epoch, failure) => { events.push({ kind: "state", state, failure }); },
        cleared: () => { events.push({ kind: "cleared" }); }
    });
    return {
        events,
        subscription,
        values: () => events.flatMap(event => (event.kind === "value" ? [event.value] : [])),
        states: () => events.flatMap(event => (event.kind === "state" ? [event.state] : [])),
        failures: () => events.flatMap(event => (event.kind === "state" && event.failure !== undefined ? [event.failure] : []))
    };
}

export async function eventually<T>(read: () => T | undefined | false, what: string, timeoutMs = 20_000): Promise<T> {
    const deadline = Date.now() + timeoutMs;
    for (;;) {
        const value = read();
        if (value !== undefined && value !== false) return value;
        if (Date.now() > deadline) throw new Error("Timed out waiting for " + what);
        await new Promise<void>(resolve => { setTimeout(resolve, 20); });
    }
}

export function ok(condition: boolean, what: string): void {
    if (!condition) throw new Error("Expected " + what);
}

export function expect(actual: unknown, expected: unknown, what: string): void {
    const a = JSON.stringify(actual);
    const e = JSON.stringify(expected);
    if (a !== e) throw new Error(what + ": expected " + e + " but got " + a);
}

export function newItem(overrides: Partial<CreateWorkItem> = {}): CreateWorkItem {
    return { id: crypto.randomUUID(), name: "item-" + crypto.randomUUID(), title: "first", state: "open", priority: 0, ...overrides };
}

export function pause(ms: number): void {
    Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, ms);
}

const waiting = new Map<string, (payload: string) => void>();
let buffered = "";
let listening = false;

function listen(): void {
    if (listening) return;
    listening = true;
    process.stdin.setEncoding("utf8");
    process.stdin.on("data", chunk => {
        buffered += chunk;
        for (let end = buffered.indexOf("\n"); end >= 0; end = buffered.indexOf("\n")) {
            const line = buffered.slice(0, end);
            buffered = buffered.slice(end + 1);
            if (!line.startsWith("go:")) continue;
            const rest = line.slice(3);
            const space = rest.indexOf(" ");
            const label = space < 0 ? rest : rest.slice(0, space);
            waiting.get(label)?.(space < 0 ? "" : rest.slice(space + 1));
            waiting.delete(label);
        }
    });
}

/** Asks the Java side to act (`await:<label>` on stdout) and resolves with its reply payload (`go:<label> <payload>` on stdin). */
export function java(label: string): Promise<string> {
    listen();
    return new Promise(resolve => {
        waiting.set(label, resolve);
        process.stdout.write("await:" + label + "\n");
    });
}

export function run(scenario: () => Promise<void>): void {
    scenario().then(
        () => process.exit(0),
        (error: unknown) => {
            console.error(error instanceof Error ? (error.stack ?? error.message) : String(error));
            process.exit(1);
        });
}
