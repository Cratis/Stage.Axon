// Runtime proofs for the generated HTTP transport: node --test, a fake fetch, no dependencies.
import assert from "node:assert/strict";
import { test } from "node:test";
import { descriptor, problem, transportFor } from "./support.mjs";

test("an absent zero-or-one snapshot answered with an empty 404 is ok(null)", async () => {
    const outcome = await transportFor(async () => new Response(null, { status: 404 })).snapshot(descriptor, {});
    assert.deepEqual(outcome, { status: "ok", value: null });
});

test("a problem body on a 404 stays a failure for a zero-or-one snapshot", async () => {
    const outcome = await transportFor(async () => problem(404, "query-result-missing")).snapshot(descriptor, {});
    assert.deepEqual(outcome, { status: "notFound" });
    const other = await transportFor(async () => new Response(JSON.stringify({ detail: "x" }), { status: 404 })).snapshot(descriptor, {});
    assert.equal(other.status, "failed");
});

test("an empty 404 is still a failure when the query cannot be absent", async () => {
    const { absent: _ignored, ...required } = descriptor;
    const outcome = await transportFor(async () => new Response(null, { status: 404 })).snapshot(required, {});
    assert.equal(outcome.status, "failed");
});

test("aborting while an error body is streaming reports cancelled, not failed", async () => {
    const controller = new AbortController();
    const fetch = async (_input, init) => {
        const body = new ReadableStream({
            start(stream) {
                stream.enqueue(new TextEncoder().encode("{\"type\":"));
                init.signal.addEventListener("abort", () => stream.error(init.signal.reason), { once: true });
            }
        });
        return new Response(body, { status: 500 });
    };
    const pending = transportFor(fetch).snapshot(descriptor, {}, controller.signal);
    setTimeout(() => controller.abort(), 10);
    assert.deepEqual(await pending, { status: "cancelled", reason: "aborted" });
});

test("an error response with no cancellation still reports its problem", async () => {
    const outcome = await transportFor(async () => problem(500, "other")).snapshot(descriptor, {});
    assert.equal(outcome.status, "failed");
});
