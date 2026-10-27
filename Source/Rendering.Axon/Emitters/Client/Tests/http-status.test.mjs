// Status mapping, credentials, void commands, cancellation and the cleanup in `run`, over a fake fetch.
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { DecodeError, command, decodeSafeInteger, descriptor, hangingFetch, liveQuery, problem, scriptedFetch, templates, transportFor } from "./support.mjs";

const answer = (response) => async () => response;

const rows = [
    ["401", () => new Response(null, { status: 401 }), { status: "unauthenticated" }],
    ["401 with a problem body", () => problem(401, "validation"), { status: "unauthenticated" }],
    ["403", () => new Response(null, { status: 403 }), { status: "forbidden" }],
    ["validation", () => problem(400, "validation", { errors: [{ property: "name", message: "required" }, { property: 1 }, "x"] }), { status: "validation", errors: [{ property: "name", message: "required" }] }],
    ["conflict", () => problem(409, "conflict"), { status: "conflict", problem: { type: "urn:cratis:stage-axon:problem:conflict", status: 409, detail: "d" } }],
    ["query result missing", () => problem(404, "query-result-missing"), { status: "notFound" }],
    ["query cardinality", () => problem(500, "query-cardinality", { count: 3 }), { status: "cardinality", count: 3 }],
    ["live capacity with Retry-After", () => problem(503, "live-capacity", {}, { "Retry-After": "7" }), { status: "unavailable", retryAfterSeconds: 7 }],
    ["live capacity without Retry-After", () => problem(503, "live-capacity"), { status: "unavailable", retryAfterSeconds: undefined }],
    ["503 with Retry-After", () => new Response(null, { status: 503, headers: { "Retry-After": "30" } }), { status: "unavailable", retryAfterSeconds: 30 }],
    ["503 with an unreadable Retry-After", () => new Response(null, { status: 503, headers: { "Retry-After": "soon" } }), { status: "unavailable", retryAfterSeconds: undefined }],
    ["503 with a negative Retry-After", () => new Response(null, { status: 503, headers: { "Retry-After": "-1" } }), { status: "unavailable", retryAfterSeconds: undefined }],
    ["an unknown problem", () => problem(500, "other"), { status: "failed", problem: { type: "urn:cratis:stage-axon:problem:other", status: 500, detail: "d" } }],
    ["a non-JSON body", () => new Response("<html>", { status: 502 }), { status: "failed", problem: { type: undefined, status: 502, detail: undefined } }],
    ["a non-object JSON body", () => new Response("[1]", { status: 500 }), { status: "failed", problem: { type: undefined, status: 500, detail: undefined } }]
];

for (const [name, make, expected] of rows) {
    test("status row: " + name + " (snapshot)", async () => {
        assert.deepEqual(await transportFor(answer(make())).snapshot(liveQuery, {}), expected);
    });
}

test("the same rows hold for commands", async () => {
    for (const [, make, expected] of rows) assert.deepEqual(await transportFor(answer(make())).execute(command, {}), expected);
});

test("an unknown route fails without calling fetch", async () => {
    const fetch = scriptedFetch();
    const outcome = await transportFor(fetch).execute({ id: "POST /missing" }, {});
    assert.deepEqual(outcome, { status: "failed", problem: undefined });
    assert.equal(fetch.calls.length, 0);
});

test("every request sets credentials omit and never sends a Cookie header", async () => {
    const fetch = scriptedFetch(answer(new Response(null, { status: 204 })), answer(new Response("[]")), async () => { throw new Error("stop"); });
    const transport = transportFor(fetch, { authorization: () => "Bearer t", scheduler: { schedule: () => () => undefined } });
    await transport.execute(command, {});
    await transport.snapshot(liveQuery, {});
    transport.watch(liveQuery, {}, { value() {}, state() {}, cleared() {} });
    for (let turn = 0; turn < 20 && fetch.calls.length < 3; turn++) await new Promise(resolve => setImmediate(resolve));
    assert.equal(fetch.calls.length, 3);
    for (const call of fetch.calls) {
        assert.equal(call.init.credentials, "omit");
        assert.equal(Object.keys(call.init.headers).some(name => name.toLowerCase() === "cookie"), false);
        assert.equal(call.init.headers["Authorization"], "Bearer t");
    }
});

test("a void command answers ok(undefined) for an empty 200 and for 204", async () => {
    for (const status of [200, 204]) {
        const outcome = await transportFor(answer(new Response(status === 204 ? null : "", { status }))).execute(command, {});
        assert.deepEqual(outcome, { status: "ok", value: undefined });
        assert.ok("value" in outcome);
    }
});

test("a void command ignores a body it did not ask for", async () => {
    assert.deepEqual(await transportFor(answer(new Response("{\"x\":1}"))).execute(command, {}), { status: "ok", value: undefined });
});

test("a command with a decoder returns the decoded body", async () => {
    const decoded = { id: "POST /do", decode: (value) => value.n };
    assert.deepEqual(await transportFor(answer(new Response("{\"n\":4}"))).execute(decoded, {}), { status: "ok", value: 4 });
});

test("an unsafe integer is a DecodeError, which the transport reports as failed", async () => {
    assert.throws(() => decodeSafeInteger(2 ** 53, "$.n"), DecodeError);
    assert.throws(() => decodeSafeInteger(1.5, "$.n"), DecodeError);
    assert.equal(decodeSafeInteger(2 ** 53 - 1, "$.n"), 2 ** 53 - 1);
    const query = { ...liveQuery, decode: (value, path) => decodeSafeInteger(value, path) };
    const outcome = await transportFor(answer(new Response("9007199254740993"))).snapshot(query, {});
    assert.equal(outcome.status, "failed");
    assert.match(outcome.problem.detail, /safe integer/);
});

test("a body that is not JSON is failed without a problem", async () => {
    assert.deepEqual(await transportFor(answer(new Response("{nope"))).snapshot(liveQuery, {}), { status: "failed", problem: undefined });
});

test("a thrown fetch is failed without a problem", async () => {
    const outcome = await transportFor(async () => { throw new TypeError("network"); }).execute(command, {});
    assert.deepEqual(outcome, { status: "failed", problem: undefined });
});

test("a signal aborted before the call is cancelled and never reaches fetch", async () => {
    const fetch = scriptedFetch();
    const controller = new AbortController();
    controller.abort();
    assert.deepEqual(await transportFor(fetch).snapshot(liveQuery, {}, controller.signal), { status: "cancelled", reason: "aborted" });
    assert.equal(fetch.calls.length, 0);
});

test("an abort while the request is in flight is cancelled(aborted)", async () => {
    const fetch = hangingFetch();
    const controller = new AbortController();
    const pending = transportFor(fetch).execute(command, {}, controller.signal);
    controller.abort();
    assert.deepEqual(await pending, { status: "cancelled", reason: "aborted" });
    assert.equal(fetch.calls[0].init.signal.aborted, true);
});

test("changeIdentity resolves an in-flight request as cancelled(identity) and bumps the epoch", async () => {
    const transport = transportFor(hangingFetch());
    const pending = transport.snapshot(liveQuery, {});
    assert.equal(transport.epoch, 0);
    transport.changeIdentity();
    assert.equal(transport.epoch, 1);
    assert.deepEqual(await pending, { status: "cancelled", reason: "identity" });
});

test("a finished request leaves nothing attached to the caller or identity signals", async () => {
    for (const make of [() => new Response("[]"), () => { throw new TypeError("network"); }]) {
        const fetch = scriptedFetch(async () => make());
        const transport = transportFor(fetch);
        const controller = new AbortController();
        await transport.snapshot(liveQuery, {}, controller.signal);
        const linked = fetch.calls[0].init.signal;
        controller.abort();
        transport.changeIdentity();
        assert.equal(linked.aborted, false);
    }
});

test("an abort that lands while the answer is being read still reports cancelled", async () => {
    const controller = new AbortController();
    const outcome = await transportFor(async () => {
        controller.abort();
        return new Response("[]");
    }).snapshot(liveQuery, {}, controller.signal);
    assert.deepEqual(outcome, { status: "cancelled", reason: "aborted" });
});

test("transport.ts imports nothing", () => {
    const source = readFileSync(new URL("transport.ts", templates), "utf8");
    assert.equal(/^\s*import\b/m.test(source), false);
    assert.equal(/\bimport\s*\(/.test(source), false);
});

test("descriptor with absent is untouched by status rows", async () => {
    assert.deepEqual(await transportFor(answer(new Response("null"))).snapshot(descriptor, {}), { status: "ok", value: null });
});
