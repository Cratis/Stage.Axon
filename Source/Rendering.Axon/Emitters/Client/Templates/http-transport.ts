import { problemTypes } from "./problems.ts";
import { readFrames } from "./sse.ts";
import {
    DecodeError,
    type CommandDescriptor,
    type LiveState,
    type Outcome,
    type Problem,
    type QueryDescriptor,
    type Subscription,
    type Transport,
    type ValidationFailure,
    type WatchObserver
} from "./transport.ts";

/** One endpoint of the generated server, as listed in http-contract.json plus its response kind. */
export interface Route {
    /** Method and path, followed by the media type for a stream. */
    readonly id: string;
    readonly method: string;
    readonly path: string;
    readonly produces: readonly string[];
    readonly kind: "command" | "query" | "stream";
    readonly consumes: readonly string[];
    readonly responses: readonly string[];
    readonly parameters: readonly { readonly name: string; readonly location: string; readonly required: boolean }[];
    readonly cardinality: "many" | "one" | "zeroOrOne" | null;
    readonly authorization: string | null;
    readonly problems: readonly { readonly type: string; readonly status: number }[];
    readonly response: "json" | "none" | "stream";
}

/** Schedules deferred work; the returned function cancels it. */
export interface Scheduler {
    schedule(callback: () => void, delayMs: number): () => void;
}

export interface HttpTransportOptions {
    readonly baseUrl: string;
    /** Returns the value of the credentials header, read again for every request; return `undefined` to send none. */
    readonly authorization: () => string | undefined;
    readonly fetch?: typeof globalThis.fetch;
    readonly scheduler?: Scheduler;
    /** The largest accepted stream frame, in characters. Defaults to one million. */
    readonly maxFrameChars?: number;
    /** The server heartbeat interval. A stream that is silent for three intervals is reopened. Defaults to 15 seconds. */
    readonly heartbeatMs?: number;
}

const timerScheduler: Scheduler = {
    schedule(callback, delayMs) {
        const handle = setTimeout(callback, delayMs);
        return () => clearTimeout(handle);
    }
};

const streamType = "text/event-stream";
const maxBackoffMs = 30_000;
const maxRetryAfterMs = 300_000;

export function createHttpTransport(routes: readonly Route[], options: HttpTransportOptions): Transport {
    return new HttpTransport(routes, options);
}

// A function call, so the compiler does not carry an earlier `aborted` check across the awaited work.
function isAborted(signal: AbortSignal | undefined): boolean {
    return signal?.aborted === true;
}

function cancelled<T>(reason: "aborted" | "identity"): Outcome<T> {
    return { status: "cancelled", reason };
}

// A command without a decoder returns no body, so its result type is void and `undefined` is the only value.
function noValue<R>(): R {
    return undefined as R;
}

// `text` is the body when the caller already read it.
async function readProblem(response: Response, text?: string): Promise<{ problem: Problem; errors: readonly ValidationFailure[]; count: number }> {
    let body: Record<string, unknown> = {};
    try {
        const parsed: unknown = JSON.parse(text ?? await response.text());
        if (typeof parsed === "object" && parsed !== null && !Array.isArray(parsed)) body = Object.fromEntries(Object.entries(parsed));
    } catch {
        // An empty or non-JSON body carries no problem details.
    }

    const errors: ValidationFailure[] = [];
    if (Array.isArray(body["errors"])) {
        for (const entry of body["errors"]) {
            if (typeof entry === "object" && entry !== null && "property" in entry && "message" in entry && typeof entry.property === "string" && typeof entry.message === "string")
                errors.push({ property: entry.property, message: entry.message });
        }
    }

    const type = typeof body["type"] === "string" ? body["type"] : undefined;
    const detail = typeof body["detail"] === "string" ? body["detail"] : undefined;
    const count = typeof body["count"] === "number" ? body["count"] : 0;
    return { problem: { type, status: response.status, detail }, errors, count };
}

function retryAfterSeconds(response: Response): number | undefined {
    const header = response.headers.get("Retry-After");
    if (header === null) return undefined;
    const seconds = Number(header);
    return Number.isFinite(seconds) && seconds >= 0 ? seconds : undefined;
}

async function failure<T>(response: Response, text?: string): Promise<Outcome<T>> {
    if (response.status === 401) return { status: "unauthenticated" };
    if (response.status === 403) return { status: "forbidden" };
    const { problem, errors, count } = await readProblem(response, text);
    switch (problem.type) {
        case problemTypes.validation: return { status: "validation", errors };
        case problemTypes.conflict: return { status: "conflict", problem };
        case problemTypes.queryResultMissing: return { status: "notFound" };
        case problemTypes.queryCardinality: return { status: "cardinality", count };
        case problemTypes.liveCapacity: return { status: "unavailable", retryAfterSeconds: retryAfterSeconds(response) };
        default: break;
    }

    if (response.status === 503) return { status: "unavailable", retryAfterSeconds: retryAfterSeconds(response) };
    return { status: "failed", problem };
}

class HttpTransport implements Transport {
    epoch = 0;
    readonly baseUrl: string;
    readonly authorization: () => string | undefined;
    readonly fetcher: typeof globalThis.fetch;
    readonly scheduler: Scheduler;
    readonly maxFrameChars: number;
    readonly heartbeatMs: number;
    readonly routes = new Map<string, Route>();
    readonly watches = new Set<Watch<object, unknown>>();
    identity = new AbortController();

    constructor(routes: readonly Route[], options: HttpTransportOptions) {
        for (const route of routes) this.routes.set(route.id, route);
        this.baseUrl = options.baseUrl.replace(/\/+$/, "");
        this.authorization = options.authorization;
        const supplied = options.fetch;
        // Call through a wrapper: the platform fetch throws when invoked with the transport as `this`.
        this.fetcher = supplied === undefined ? (input, init) => globalThis.fetch(input, init) : (input, init) => supplied(input, init);
        this.scheduler = options.scheduler ?? timerScheduler;
        this.maxFrameChars = options.maxFrameChars ?? 1_000_000;
        this.heartbeatMs = options.heartbeatMs ?? 15_000;
    }

    changeIdentity(): void {
        this.epoch += 1;
        const previous = this.identity;
        this.identity = new AbortController();
        previous.abort();
        for (const watch of [...this.watches]) watch.reopen();
    }

    headers(accept: string, contentType?: string): Record<string, string> {
        const headers: Record<string, string> = { Accept: accept, "X-Requested-With": "stage-axon-client" };
        if (contentType !== undefined) headers["Content-Type"] = contentType;
        const credentials = this.authorization();
        if (credentials !== undefined) headers["Authorization"] = credentials;
        return headers;
    }

    url(route: Route, key: object | undefined): string {
        const parameters = new URLSearchParams();
        if (key !== undefined) {
            for (const [name, value] of Object.entries(key)) if (value !== undefined && value !== null) parameters.append(name, String(value));
        }

        const query = parameters.toString();
        return this.baseUrl + route.path + (query === "" ? "" : "?" + query);
    }

    execute<C, R>(command: CommandDescriptor<C, R>, body: C, signal?: AbortSignal): Promise<Outcome<R>> {
        const route = this.routes.get(command.id);
        if (route === undefined) return Promise.resolve({ status: "failed", problem: undefined });
        return this.run(signal, async (linked) => {
            const response = await this.fetcher(this.url(route, undefined), {
                method: "POST",
                headers: this.headers("application/json", "application/json"),
                body: JSON.stringify(body),
                credentials: "omit",
                signal: linked
            });
            if (!response.ok) return failure<R>(response);
            if (command.decode === undefined) return { status: "ok", value: noValue<R>() };
            return { status: "ok", value: command.decode(JSON.parse(await response.text()), "$") };
        });
    }

    snapshot<K extends object, R>(query: QueryDescriptor<K, R>, key: K, signal?: AbortSignal): Promise<Outcome<R>> {
        const route = this.routes.get(query.id);
        if (route === undefined) return Promise.resolve({ status: "failed", problem: undefined });
        return this.run(signal, async (linked) => {
            const response = await this.fetcher(this.url(route, key), {
                method: "GET",
                headers: this.headers("application/json"),
                credentials: "omit",
                signal: linked
            });
            if (!response.ok) {
                if (response.status !== 404 || query.absent === undefined) return failure<R>(response);
                // A zero-or-one query that finds nothing answers 404 with an empty body; a problem body stays a failure.
                const missing = await response.text();
                return missing.trim() === "" ? { status: "ok", value: query.absent() } : failure<R>(response, missing);
            }

            const text = await response.text();
            // A zero-or-one query that finds nothing answers with an empty body or null.
            if (query.absent !== undefined && (text.trim() === "" || text.trim() === "null")) return { status: "ok", value: query.absent() };
            return { status: "ok", value: query.decode(JSON.parse(text), "$") };
        });
    }

    watch<K extends object, R>(query: QueryDescriptor<K, R>, key: K, observer: WatchObserver<R>, signal?: AbortSignal): Subscription {
        const route = this.routes.get(query.id + " " + streamType);
        const watch = new Watch<K, R>(this, route, query, key, observer);
        this.watches.add(watch);
        signal?.addEventListener("abort", () => watch.close(), { once: true });
        if (signal?.aborted === true) watch.close();
        else watch.open();
        return watch;
    }

    // Runs one request linked to the caller's signal and to the current identity, and maps a thrown failure to an outcome.
    async run<R>(caller: AbortSignal | undefined, work: (signal: AbortSignal) => Promise<Outcome<R>>): Promise<Outcome<R>> {
        if (caller?.aborted === true) return cancelled("aborted");
        const epoch = this.epoch;
        const link = new AbortController();
        const abort = () => link.abort();
        caller?.addEventListener("abort", abort, { once: true });
        this.identity.signal.addEventListener("abort", abort, { once: true });
        try {
            const outcome = await work(link.signal);
            // Reading an error body swallows an abort, so cancellation is checked again before any outcome is returned.
            if (epoch !== this.epoch) return cancelled("identity");
            return isAborted(caller) ? cancelled("aborted") : outcome;
        } catch (error) {
            if (epoch !== this.epoch) return cancelled("identity");
            if (isAborted(caller)) return cancelled("aborted");
            if (error instanceof DecodeError) return { status: "failed", problem: { type: undefined, status: 0, detail: error.message } };
            return { status: "failed", problem: undefined };
        } finally {
            caller?.removeEventListener("abort", abort);
            this.identity.signal.removeEventListener("abort", abort);
        }
    }
}

// One live query. Every asynchronous step checks its generation, so a closed, reopened or superseded connection never delivers.
class Watch<K extends object, R> implements Subscription {
    readonly owner: HttpTransport;
    readonly route: Route | undefined;
    readonly query: QueryDescriptor<K, R>;
    readonly key: K;
    readonly observer: WatchObserver<R>;
    generation = 0;
    attempts = 0;
    closed = false;
    connection: AbortController | undefined;
    cancelRetry: (() => void) | undefined;
    cancelIdle: (() => void) | undefined;

    constructor(owner: HttpTransport, route: Route | undefined, query: QueryDescriptor<K, R>, key: K, observer: WatchObserver<R>) {
        this.owner = owner;
        this.route = route;
        this.query = query;
        this.key = key;
        this.observer = observer;
    }

    open(): void {
        if (this.route === undefined || !this.query.live) this.state("failed");
        else void this.connect();
    }

    // The identity changed: drop the old value and stream, and open again under the new credentials.
    reopen(): void {
        if (this.closed) return;
        this.stop();
        this.attempts = 0;
        this.observer.cleared(this.owner.epoch);
        this.open();
    }

    close(): void {
        if (this.closed) return;
        this.closed = true;
        this.stop();
        this.owner.watches.delete(this);
        this.observer.state("closed", this.owner.epoch);
    }

    stop(): void {
        this.generation += 1;
        this.cancelRetry?.();
        this.cancelIdle?.();
        this.cancelRetry = undefined;
        this.cancelIdle = undefined;
        this.connection?.abort();
        this.connection = undefined;
    }

    state(state: LiveState): void {
        this.observer.state(state, this.owner.epoch);
    }

    async connect(): Promise<void> {
        this.stop();
        const generation = this.generation;
        const epoch = this.owner.epoch;
        const current = () => !this.closed && generation === this.generation && epoch === this.owner.epoch;
        const connection = new AbortController();
        this.connection = connection;
        this.state(this.attempts === 0 ? "connecting" : "reconnecting");
        try {
            const response = await this.owner.fetcher(this.owner.url(this.route!, this.key), {
                method: "GET",
                headers: this.owner.headers(streamType),
                credentials: "omit",
                signal: connection.signal
            });
            if (!current()) return;
            if (response.status === 401 || response.status === 403) {
                this.stop();
                this.state("failed");
                return;
            }

            if (!response.ok || response.body === null) {
                await this.rejected(response, current);
                return;
            }

            this.watchdog(connection, generation);
            await this.deliver(response.body, connection, generation, epoch, current);
            if (current()) this.retry(undefined);
        } catch (error) {
            if (current()) this.failed(error);
        }
    }

    // The server refused the stream: reconnect after its Retry-After, or the backoff.
    async rejected(response: Response, current: () => boolean): Promise<void> {
        const seconds = retryAfterSeconds(response);
        await response.body?.cancel().catch(() => undefined);
        if (current()) this.retry(seconds === undefined ? undefined : Math.min(seconds * 1000, maxRetryAfterMs));
    }

    // Hands each frame of the open stream to the observer while the connection is still current.
    async deliver(body: ReadableStream<Uint8Array>, connection: AbortController, generation: number, epoch: number, current: () => boolean): Promise<void> {
        let first = true;
        for await (const frame of readFrames(body, this.owner.maxFrameChars)) {
            if (!current()) return;
            this.watchdog(connection, generation);
            if (frame.kind === "comment") continue;
            const value = this.decodeFrame(frame.data);
            if (first) {
                first = false;
                this.attempts = 0;
                this.state("live");
            }

            this.observer.value(value, epoch);
        }
    }

    decodeFrame(data: string): R {
        const payload: unknown = JSON.parse(data);
        return payload === null && this.query.absent !== undefined ? this.query.absent() : this.query.decode(payload, "$");
    }

    // A malformed frame ends the watch; any other failure reconnects.
    failed(error: unknown): void {
        if (error instanceof DecodeError || error instanceof SyntaxError) {
            this.stop();
            this.state("failed");
        } else {
            this.retry(undefined);
        }
    }

    // Reopens a stream that stayed silent for three heartbeats; comment frames count as activity.
    watchdog(connection: AbortController, generation: number): void {
        this.cancelIdle?.();
        this.cancelIdle = this.owner.scheduler.schedule(() => {
            if (generation === this.generation) connection.abort();
        }, this.owner.heartbeatMs * 3);
    }

    retry(serverDelayMs: number | undefined): void {
        this.stop();
        const delay = serverDelayMs ?? Math.min(maxBackoffMs, 1000 * 2 ** this.attempts);
        this.attempts += 1;
        this.state("reconnecting");
        const generation = this.generation;
        this.cancelRetry = this.owner.scheduler.schedule(() => {
            if (generation === this.generation && !this.closed) void this.connect();
        }, delay);
    }
}
