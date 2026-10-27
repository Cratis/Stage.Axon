// Shared harness for the transport proofs: the templates copied next to a problems.ts, a scripted fetch and a manual scheduler.
// There are no timers and no dependencies; asynchronous progress is awaited with `until`.
import assert from "node:assert/strict";
import { copyFileSync, mkdtempSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { pathToFileURL } from "node:url";

export const templates = new URL("../Templates/", import.meta.url);
const dir = mkdtempSync(join(tmpdir(), "axon-client-"));
for (const name of ["transport", "http-transport", "sse"]) copyFileSync(new URL(name + ".ts", templates), join(dir, name + ".ts"));
export const urn = (type) => "urn:cratis:stage-axon:problem:" + type;
writeFileSync(join(dir, "problems.ts"), `export const problemTypes = {
    conflict: "${urn("conflict")}",
    liveCapacity: "${urn("live-capacity")}",
    queryCardinality: "${urn("query-cardinality")}",
    queryResultMissing: "${urn("query-result-missing")}",
    validation: "${urn("validation")}"
} as const;
`);
const load = (name) => import(pathToFileURL(join(dir, name + ".ts")).href);
export const { createHttpTransport } = await load("http-transport");
export const { readFrames, FrameTooLarge } = await load("sse");
export const { DecodeError, decodeSafeInteger } = await load("transport");

export const route = { id: "GET /rows", method: "GET", path: "/rows", produces: [], kind: "query", consumes: [], responses: [], parameters: [], cardinality: "zeroOrOne", authorization: null, problems: [], response: "json" };
export const streamRoute = { ...route, id: "GET /rows text/event-stream", produces: ["text/event-stream"], kind: "stream", response: "stream" };
export const commandRoute = { ...route, id: "POST /do", method: "POST", path: "/do", kind: "command", cardinality: null };
export const routes = [route, streamRoute, commandRoute];
export const descriptor = { id: "GET /rows", cardinality: "zeroOrOne", live: false, decode: (value) => value, absent: () => null };
export const liveQuery = { id: "GET /rows", cardinality: "many", live: true, decode: (value) => value };
export const command = { id: "POST /do" };

export const problem = (status, type, extra = {}, headers = {}) =>
    new Response(JSON.stringify({ type: type === undefined ? undefined : urn(type), detail: "d", ...extra }), { status, headers: { "Content-Type": "application/problem+json", ...headers } });

export async function until(condition) {
    for (let turn = 0; turn < 2000; turn++) {
        if (condition()) return;
        await new Promise(resolve => setImmediate(resolve));
    }
    assert.fail("the condition was never met");
}

/** A scheduler that only runs what the test fires. */
export function manualScheduler() {
    const tasks = [];
    return {
        schedule(callback, delayMs) {
            const task = { callback, delayMs, active: true };
            tasks.push(task);
            return () => { task.active = false; };
        },
        delays: () => tasks.filter(task => task.active).map(task => task.delayMs),
        fire(delayMs) {
            const task = tasks.find(candidate => candidate.active && candidate.delayMs === delayMs);
            assert.ok(task, "no task is scheduled for " + delayMs + " ms; pending: " + JSON.stringify(tasks.filter(candidate => candidate.active).map(candidate => candidate.delayMs)));
            task.active = false;
            task.callback();
        }
    };
}

/** A fetch that answers from a script, one responder per call, and records every call. */
export function scriptedFetch(...responders) {
    const calls = [];
    const fetch = async (input, init) => {
        calls.push({ url: String(input), init });
        const responder = responders[calls.length - 1];
        assert.ok(responder, "unexpected fetch #" + calls.length + " to " + input);
        return responder(init);
    };
    fetch.calls = calls;
    return fetch;
}

/** A fetch that never answers and rejects when its signal aborts, like the platform fetch. */
export function hangingFetch() {
    const calls = [];
    const fetch = (input, init) => {
        calls.push({ url: String(input), init });
        return new Promise((_resolve, reject) => init.signal.addEventListener("abort", () => reject(init.signal.reason), { once: true }));
    };
    fetch.calls = calls;
    return fetch;
}

/** A server-sent stream the test writes to. With `errorOnAbort` an abort errors the body, as the platform does. */
export function sseStream(init, { errorOnAbort = true } = {}) {
    const encoder = new TextEncoder();
    let controller;
    const body = new ReadableStream({ start(stream) { controller = stream; } });
    if (errorOnAbort) init.signal.addEventListener("abort", () => { try { controller.error(init.signal.reason); } catch { /* already closed */ } }, { once: true });
    return {
        response: () => new Response(body, { status: 200, headers: { "Content-Type": "text/event-stream" } }),
        push(text) { try { controller.enqueue(typeof text === "string" ? encoder.encode(text) : text); } catch { /* the reader is gone */ } },
        end() { controller.close(); }
    };
}

export function recorder() {
    const events = [];
    return {
        events,
        values: () => events.filter(event => event.startsWith("value:")),
        value: (value, epoch) => events.push("value:" + JSON.stringify(value) + "@" + epoch),
        state: (state, epoch) => events.push("state:" + state + "@" + epoch),
        cleared: (epoch) => events.push("cleared@" + epoch)
    };
}

export const transportFor = (fetch, overrides = {}) =>
    createHttpTransport(routes, { baseUrl: "http://server", authorization: () => undefined, fetch, ...overrides });
