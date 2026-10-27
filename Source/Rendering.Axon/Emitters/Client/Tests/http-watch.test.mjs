// Server-sent parsing and live watches: a scripted stream, a manual scheduler, no sleeps.
import assert from "node:assert/strict";
import { test } from "node:test";
import { FrameTooLarge, hangingFetch, liveQuery, manualScheduler, problem, readFrames, recorder, scriptedFetch, sseStream, transportFor, until } from "./support.mjs";

function bodyOf(...chunks) {
    const encoder = new TextEncoder();
    return new ReadableStream({
        start(stream) {
            for (const chunk of chunks) stream.enqueue(typeof chunk === "string" ? encoder.encode(chunk) : chunk);
            stream.close();
        }
    });
}

async function frames(body, limit = 1000) {
    const found = [];
    for await (const frame of readFrames(body, limit)) found.push(frame);
    return found;
}

test("frames survive being split across chunks, including inside CRLF and a multi-byte character", async () => {
    const bytes = new TextEncoder().encode("data: å\r\n\r\n");
    const split = bytes.indexOf(0xc3) + 1;
    const found = await frames(bodyOf(bytes.slice(0, split), bytes.slice(split, bytes.length - 3), bytes.slice(bytes.length - 3, bytes.length - 1), bytes.slice(bytes.length - 1)));
    assert.deepEqual(found, [{ kind: "data", data: "å" }]);
});

test("a heartbeat comment is reported and multi-line data joins with a newline", async () => {
    const found = await frames(bodyOf(": hb\n\nid: 4\nevent: x\ndata: a\ndata:b\ndata\n\n"));
    assert.deepEqual(found, [{ kind: "comment" }, { kind: "data", data: "a\nb\n" }]);
});

test("an event is only delivered at its blank line", async () => {
    assert.deepEqual(await frames(bodyOf("data: 1\n")), []);
});

test("an oversized frame throws FrameTooLarge, whether it has newlines or not", async () => {
    await assert.rejects(frames(bodyOf("data: " + "x".repeat(30) + "\n\n"), 20), FrameTooLarge);
    await assert.rejects(frames(bodyOf("data: ab\ndata: cd\ndata: ef\n\n"), 5), FrameTooLarge);
    await assert.rejects(frames(bodyOf("x".repeat(30)), 20), FrameTooLarge);
});

function open(overrides = {}) {
    const scheduler = manualScheduler();
    const streams = [];
    const responders = [];
    const fetch = scriptedFetch(...Array.from({ length: 6 }, () => async (init) => {
        const stream = sseStream(init, overrides.stream);
        streams.push(stream);
        return stream.response();
    }));
    const transport = transportFor(fetch, { scheduler, heartbeatMs: 1000, authorization: overrides.authorization ?? (() => undefined), maxFrameChars: overrides.maxFrameChars });
    const observer = recorder();
    return { scheduler, streams, fetch, transport, observer, responders };
}

test("a watch connects, goes live on the first frame and reports values with the epoch", async () => {
    const { streams, observer, transport } = open();
    transport.watch(liveQuery, { id: 1 }, observer);
    await until(() => streams.length === 1);
    streams[0].push(": hb\n\ndata: [1]\n\ndata: [2]\n\n");
    await until(() => observer.values().length === 2);
    assert.deepEqual(observer.events, ["state:connecting@0", "state:live@0", "value:[1]@0", "value:[2]@0"]);
});

test("every frame reaches the observer in order; the transport does not coalesce", async () => {
    const { streams, observer, transport } = open();
    transport.watch(liveQuery, {}, observer);
    await until(() => streams.length === 1);
    streams[0].push("data: 1\n\ndata: 2\n\ndata: 3\n\n");
    await until(() => observer.values().length === 3);
    assert.deepEqual(observer.values(), ["value:1@0", "value:2@0", "value:3@0"]);
});

test("an oversized frame ends the watch as failed", async () => {
    const { streams, observer, transport, scheduler } = open({ maxFrameChars: 20 });
    transport.watch(liveQuery, {}, observer);
    await until(() => streams.length === 1);
    streams[0].push("data: " + "x".repeat(40) + "\n\n");
    await until(() => observer.events.includes("state:failed@0"));
    assert.deepEqual(scheduler.delays(), []);
});

test("a malformed frame ends the watch as failed", async () => {
    const { streams, observer, transport } = open();
    transport.watch(liveQuery, {}, observer);
    await until(() => streams.length === 1);
    streams[0].push("data: {nope\n\n");
    await until(() => observer.events.includes("state:failed@0"));
});

test("close stops the watch: no frame, state or retry follows, and closing twice is harmless", async () => {
    const { streams, observer, transport, scheduler } = open({ stream: { errorOnAbort: false } });
    const subscription = transport.watch(liveQuery, {}, observer);
    await until(() => streams.length === 1);
    streams[0].push("data: 1\n\n");
    await until(() => observer.values().length === 1);
    subscription.close();
    subscription.close();
    streams[0].push("data: 2\n\n");
    streams[0].end();
    for (let turn = 0; turn < 20; turn++) await new Promise(resolve => setImmediate(resolve));
    assert.deepEqual(observer.events, ["state:connecting@0", "state:live@0", "value:1@0", "state:closed@0"]);
    assert.deepEqual(scheduler.delays(), []);
});

test("a response that arrives after close is dropped", async () => {
    const scheduler = manualScheduler();
    let release;
    const gate = new Promise(resolve => { release = resolve; });
    const fetch = scriptedFetch(async (init) => { await gate; return sseStream(init).response(); });
    const observer = recorder();
    const subscription = transportFor(fetch, { scheduler }).watch(liveQuery, {}, observer);
    await until(() => fetch.calls.length === 1);
    subscription.close();
    release();
    for (let turn = 0; turn < 20; turn++) await new Promise(resolve => setImmediate(resolve));
    assert.deepEqual(observer.events, ["state:connecting@0", "state:closed@0"]);
});

test("switching keys closes the old watch; its late frames never arrive", async () => {
    const { streams, observer, transport, fetch } = open({ stream: { errorOnAbort: false } });
    const other = recorder();
    const first = transport.watch(liveQuery, { id: 1 }, observer);
    await until(() => streams.length === 1);
    first.close();
    transport.watch(liveQuery, { id: 2 }, other);
    await until(() => streams.length === 2);
    streams[0].push("data: \"old\"\n\n");
    streams[1].push("data: \"new\"\n\n");
    await until(() => other.values().length === 1);
    assert.deepEqual(other.values(), ["value:\"new\"@0"]);
    assert.equal(observer.values().length, 0);
    assert.deepEqual(fetch.calls.map(call => call.url), ["http://server/rows?id=1", "http://server/rows?id=2"]);
});

test("an aborted caller signal closes the watch, and an already aborted one never opens it", async () => {
    const { streams, observer, transport, fetch } = open();
    const controller = new AbortController();
    transport.watch(liveQuery, {}, observer, controller.signal);
    await until(() => streams.length === 1);
    controller.abort();
    assert.equal(observer.events.at(-1), "state:closed@0");
    const done = new AbortController();
    done.abort();
    const late = recorder();
    transport.watch(liveQuery, {}, late, done.signal);
    assert.deepEqual(late.events, ["state:closed@0"]);
    assert.equal(fetch.calls.length, 1);
});

test("a watch without a stream route fails at once", () => {
    const observer = recorder();
    transportFor(scriptedFetch()).watch({ ...liveQuery, id: "GET /elsewhere" }, {}, observer);
    assert.deepEqual(observer.events, ["state:failed@0"]);
});

test("a stream that ends reconnects after the backoff, with a fresh snapshot and no Last-Event-ID", async () => {
    const { streams, observer, transport, scheduler, fetch } = open();
    transport.watch(liveQuery, {}, observer);
    await until(() => streams.length === 1);
    streams[0].push("id: 9\ndata: [1]\n\n");
    await until(() => observer.values().length === 1);
    streams[0].end();
    await until(() => observer.events.includes("state:reconnecting@0"));
    assert.ok(scheduler.delays().includes(1000));
    scheduler.fire(1000);
    await until(() => streams.length === 2);
    streams[1].push("data: [1,2]\n\n");
    await until(() => observer.values().length === 2);
    assert.deepEqual(observer.events.slice(-4), ["state:reconnecting@0", "state:reconnecting@0", "state:live@0", "value:[1,2]@0"]);
    for (const call of fetch.calls) assert.equal(Object.keys(call.init.headers).some(name => name.toLowerCase() === "last-event-id"), false);
});

test("the backoff doubles while the server stays down and resets after a value", async () => {
    const scheduler = manualScheduler();
    const streams = [];
    const fetch = scriptedFetch(
        async () => problem(500, "other"),
        async () => problem(500, "other"),
        async (init) => { const stream = sseStream(init); streams.push(stream); return stream.response(); },
        async () => problem(500, "other"));
    const observer = recorder();
    transportFor(fetch, { scheduler, heartbeatMs: 1000 }).watch(liveQuery, {}, observer);
    await until(() => scheduler.delays().includes(1000));
    scheduler.fire(1000);
    await until(() => scheduler.delays().includes(2000));
    scheduler.fire(2000);
    await until(() => streams.length === 1);
    streams[0].push("data: 1\n\n");
    await until(() => observer.values().length === 1);
    streams[0].end();
    await until(() => scheduler.delays().includes(1000));
});

test("a refused stream honours Retry-After in seconds", async () => {
    const scheduler = manualScheduler();
    const fetch = scriptedFetch(async () => problem(503, "live-capacity", {}, { "Retry-After": "5" }));
    transportFor(fetch, { scheduler, heartbeatMs: 1000 }).watch(liveQuery, {}, recorder());
    await until(() => scheduler.delays().includes(5000));
});

test("401 and 403 are terminal: the watch fails and nothing is scheduled", async () => {
    for (const status of [401, 403]) {
        const scheduler = manualScheduler();
        const observer = recorder();
        const fetch = scriptedFetch(async () => new Response(null, { status }));
        transportFor(fetch, { scheduler }).watch(liveQuery, {}, observer);
        await until(() => observer.events.includes("state:failed@0"));
        assert.deepEqual(scheduler.delays(), []);
        assert.equal(fetch.calls.length, 1);
    }
});

test("a stream silent for three heartbeats is reopened", async () => {
    const { streams, observer, transport, scheduler } = open();
    transport.watch(liveQuery, {}, observer);
    await until(() => streams.length === 1);
    streams[0].push("data: 1\n\n");
    await until(() => observer.values().length === 1);
    scheduler.fire(3000);
    await until(() => scheduler.delays().includes(1000));
    scheduler.fire(1000);
    await until(() => streams.length === 2);
});

test("changeIdentity clears, drops old-epoch frames, and reopens with the new Authorization", async () => {
    let token = "Bearer a";
    const { streams, observer, transport, fetch } = open({ authorization: () => token, stream: { errorOnAbort: false } });
    transport.watch(liveQuery, {}, observer);
    await until(() => streams.length === 1);
    streams[0].push("data: \"a\"\n\n");
    await until(() => observer.values().length === 1);
    token = "Bearer b";
    transport.changeIdentity();
    streams[0].push("data: \"stale\"\n\n");
    await until(() => streams.length === 2);
    streams[1].push("data: \"b\"\n\n");
    await until(() => observer.values().length === 2);
    assert.deepEqual(observer.events, ["state:connecting@0", "state:live@0", "value:\"a\"@0", "cleared@1", "state:connecting@1", "state:live@1", "value:\"b\"@1"]);
    assert.equal(fetch.calls[0].init.headers["Authorization"], "Bearer a");
    assert.equal(fetch.calls[1].init.headers["Authorization"], "Bearer b");
});

test("a closed watch is not reopened by changeIdentity", async () => {
    const { streams, observer, transport, fetch } = open();
    const subscription = transport.watch(liveQuery, {}, observer);
    await until(() => streams.length === 1);
    subscription.close();
    transport.changeIdentity();
    assert.equal(fetch.calls.length, 1);
    assert.equal(observer.events.includes("cleared@1"), false);
});

// An observer that runs `react` the first time the state is live (or the first value arrives), to act from inside the callback.
function reacting(kind, react) {
    const observer = recorder();
    let fired = false;
    const record = observer[kind];
    observer[kind] = (payload, epoch) => {
        record(payload, epoch);
        if (!fired && (kind === "value" || payload === "live")) { fired = true; react(); }
    };
    return observer;
}

const settle = async () => { for (let turn = 0; turn < 20; turn++) await new Promise(resolve => setImmediate(resolve)); };
const oldEpoch = (observer) => observer.events.filter(event => event.startsWith("value:") && event.endsWith("@0"));

test("an identity change inside state(live) delivers no value of the old epoch", async () => {
    const harness = open({ stream: { errorOnAbort: false } });
    const observer = reacting("state", () => harness.transport.changeIdentity());
    harness.transport.watch(liveQuery, {}, observer);
    await until(() => harness.streams.length === 1);
    harness.streams[0].push("data: \"old\"\n\n");
    await settle();
    assert.deepEqual(oldEpoch(observer), []);
    assert.equal(observer.events.includes("cleared@1"), true);
});

test("an identity change inside state(live) delivers none of the frames of the same chunk", async () => {
    const harness = open({ stream: { errorOnAbort: false } });
    const observer = reacting("state", () => harness.transport.changeIdentity());
    harness.transport.watch(liveQuery, {}, observer);
    await until(() => harness.streams.length === 1);
    harness.streams[0].push("data: 1\n\ndata: 2\n\n");
    await settle();
    assert.deepEqual(oldEpoch(observer), []);
});

test("an identity change inside a value callback delivers no later frame of that chunk", async () => {
    const harness = open({ stream: { errorOnAbort: false } });
    const observer = reacting("value", () => harness.transport.changeIdentity());
    harness.transport.watch(liveQuery, {}, observer);
    await until(() => harness.streams.length === 1);
    harness.streams[0].push("data: 1\n\ndata: 2\n\ndata: 3\n\n");
    await settle();
    assert.deepEqual(oldEpoch(observer), ["value:1@0"]);
});

test("closing the watch inside state(live) delivers no value", async () => {
    let subscription;
    const harness = open({ stream: { errorOnAbort: false } });
    const observer = reacting("state", () => subscription.close());
    subscription = harness.transport.watch(liveQuery, {}, observer);
    await until(() => harness.streams.length === 1);
    harness.streams[0].push("data: 1\n\n");
    await settle();
    assert.equal(observer.values().length, 0);
});

test("aborting the caller signal inside state(live) delivers no value", async () => {
    const controller = new AbortController();
    const harness = open({ stream: { errorOnAbort: false } });
    const observer = reacting("state", () => controller.abort());
    harness.transport.watch(liveQuery, {}, observer, controller.signal);
    await until(() => harness.streams.length === 1);
    harness.streams[0].push("data: 1\n\n");
    await settle();
    assert.equal(observer.values().length, 0);
});

test("a fetch that never answers times out after three heartbeats and reconnects", async () => {
    const scheduler = manualScheduler();
    const fetch = hangingFetch();
    const observer = recorder();
    transportFor(fetch, { scheduler, heartbeatMs: 1000 }).watch(liveQuery, {}, observer);
    await until(() => fetch.calls.length === 1);
    scheduler.fire(3000);
    await until(() => scheduler.delays().includes(1000));
    assert.equal(fetch.calls[0].init.signal.aborted, true);
    assert.deepEqual(observer.events, ["state:connecting@0", "state:reconnecting@0"]);
    scheduler.fire(1000);
    await until(() => fetch.calls.length === 2);
});

test("closing a watch whose fetch never answers aborts it and leaves nothing scheduled", async () => {
    const scheduler = manualScheduler();
    const fetch = hangingFetch();
    const observer = recorder();
    const subscription = transportFor(fetch, { scheduler, heartbeatMs: 1000 }).watch(liveQuery, {}, observer);
    await until(() => fetch.calls.length === 1);
    subscription.close();
    await settle();
    assert.equal(fetch.calls[0].init.signal.aborted, true);
    assert.deepEqual(scheduler.delays(), []);
    assert.deepEqual(observer.events, ["state:connecting@0", "state:closed@0"]);
});

// A scheduler that also counts how many idle timers were armed, so a reset is visible.
function countingScheduler() {
    const scheduler = manualScheduler();
    const schedule = scheduler.schedule;
    scheduler.armed = 0;
    scheduler.schedule = (callback, delayMs) => { if (delayMs === 3000) scheduler.armed += 1; return schedule(callback, delayMs); };
    return scheduler;
}

test("a heartbeat and a data frame each reset the idle watchdog, replacing the pending timer", async () => {
    const scheduler = countingScheduler();
    const streams = [];
    const fetch = scriptedFetch(async (init) => { const stream = sseStream(init); streams.push(stream); return stream.response(); });
    const observer = recorder();
    transportFor(fetch, { scheduler, heartbeatMs: 1000 }).watch(liveQuery, {}, observer);
    await until(() => streams.length === 1);
    await settle();
    const before = scheduler.armed;
    streams[0].push(": hb\n\n");
    await until(() => scheduler.armed === before + 1);
    streams[0].push("data: 1\n\n");
    await until(() => observer.values().length === 1);
    assert.equal(scheduler.armed, before + 2);
    assert.deepEqual(scheduler.delays().filter(delay => delay === 3000), [3000]);
});

test("an identity change inside state(connecting) leaves the new connection's watchdog armed and no stale timer", async () => {
    const scheduler = manualScheduler();
    const fetch = hangingFetch();
    const observer = recorder();
    const record = observer.state;
    let changed = false;
    observer.state = (state, epoch) => {
        record(state, epoch);
        if (!changed && state === "connecting") { changed = true; transport.changeIdentity(); }
    };
    const transport = transportFor(fetch, { scheduler, heartbeatMs: 1000 });
    transport.watch(liveQuery, {}, observer);
    await until(() => fetch.calls.length === 1);
    await settle();
    assert.equal(fetch.calls.length, 1);
    assert.deepEqual(scheduler.delays(), [3000]);
    scheduler.fire(3000);
    await until(() => scheduler.delays().includes(1000));
    assert.equal(fetch.calls[0].init.signal.aborted, true);
    assert.deepEqual(scheduler.delays(), [1000]);
});

// Every observer callback, with how to reach it. `fetches` is how many fetches are expected after an identity change or after a close/abort.
const callbacks = [
    { trigger: "state:connecting", fetches: { identity: 1, away: 0 } },
    { trigger: "state:live", fetches: { identity: 2, away: 1 }, data: "data: 1\n\ndata: 2\n\n" },
    { trigger: "value:1", fetches: { identity: 2, away: 1 }, data: "data: 1\n\ndata: 2\n\n" },
    { trigger: "state:reconnecting", fetches: { identity: 2, away: 1 }, refuse: true },
    { trigger: "cleared", fetches: { identity: 2, away: 1 }, data: "data: 1\n\n", clear: true }
];
const actions = {
    identity: (transport) => transport.changeIdentity(),
    close: (transport) => [...transport.watches][0].close(),
    abort: (_transport, controller) => controller.abort()
};

for (const scenario of callbacks) {
    for (const [name, act] of Object.entries(actions)) {
        test("a " + name + " inside " + scenario.trigger + " stops the old attempt and leaves one working current attempt", async () => {
            const scheduler = manualScheduler();
            const streams = [];
            const fetch = scriptedFetch(...Array.from({ length: 6 }, (_, index) => async (init) => {
                if (scenario.refuse && index === 0) return new Response("", { status: 503 });
                const stream = sseStream(init);
                streams.push(stream);
                return stream.response();
            }));
            const transport = transportFor(fetch, { scheduler, heartbeatMs: 1000 });
            const controller = new AbortController();
            const observer = recorder();
            const wrapped = {};
            let fired = false;
            let triggerEpoch = -1;
            let triggerIndex = -1;
            for (const kind of ["state", "value", "cleared"]) {
                const original = observer[kind];
                wrapped[kind] = (...args) => {
                    original(...args);
                    const event = observer.events[observer.events.length - 1];
                    if (!fired && event.startsWith(scenario.trigger)) {
                        fired = true;
                        triggerIndex = observer.events.length;
                        triggerEpoch = Number(event.split("@")[1]);
                        act(transport, controller);
                    }
                };
            }

            transport.watch(liveQuery, {}, wrapped, controller.signal);
            if (scenario.clear) {
                await until(() => streams.length === 1);
                streams[0].push(scenario.data);
                await until(() => observer.values().length === 1);
                transport.changeIdentity();
            } else if (scenario.data !== undefined) {
                await until(() => streams.length === 1);
                streams[0].push(scenario.data);
            }

            await until(() => fired);
            await settle();
            assert.equal(fetch.calls.length, name === "identity" ? scenario.fetches.identity : scenario.fetches.away);
            const later = observer.events.slice(triggerIndex);
            const idle = scheduler.delays().filter(delay => delay === 3000);
            if (name === "identity") {
                assert.ok(later.every(event => Number(event.split("@")[1]) > triggerEpoch), "stale callback after identity change: " + later);
                assert.equal(idle.length <= 1, true);
                const before = observer.values().length;
                streams[streams.length - 1].push("data: \"new\"\n\n");
                await until(() => observer.values().length === before + 1);
                assert.equal(observer.events[observer.events.length - 1], "value:\"new\"@" + (triggerEpoch + 1));
                assert.equal(scheduler.delays().filter(delay => delay === 3000).length, 1);
            } else {
                assert.ok(later.every(event => event.startsWith("state:closed")), "callback after " + name + ": " + later);
                assert.deepEqual(scheduler.delays(), []);
            }
        });
    }
}

function failures() {
    const seen = [];
    return { seen, value() {}, cleared() {}, state: (state, _epoch, failure) => { if (failure !== undefined) seen.push({ state, failure }); } };
}

test("401 and 403 report the typed failure on the terminal state", async () => {
    for (const [status, kind] of [[401, "unauthenticated"], [403, "forbidden"]]) {
        const observer = failures();
        transportFor(scriptedFetch(async () => new Response(null, { status })), { scheduler: manualScheduler() }).watch(liveQuery, {}, observer);
        await until(() => observer.seen.length === 1);
        assert.deepEqual(observer.seen, [{ state: "failed", failure: { status: kind } }]);
    }
});

test("a refusal with Retry-After reconnects as unavailable carrying the seconds", async () => {
    const observer = failures();
    transportFor(scriptedFetch(async () => problem(503, "live-capacity", {}, { "Retry-After": "5" })), { scheduler: manualScheduler() }).watch(liveQuery, {}, observer);
    await until(() => observer.seen.length === 1);
    assert.deepEqual(observer.seen, [{ state: "reconnecting", failure: { status: "unavailable", retryAfterSeconds: 5 } }]);
});

test("a refusal without Retry-After and an ordinary reconnect carry no failure", async () => {
    const observer = failures();
    const scheduler = manualScheduler();
    transportFor(scriptedFetch(async () => new Response("", { status: 500 })), { scheduler }).watch(liveQuery, {}, observer);
    await until(() => scheduler.delays().length === 1);
    assert.deepEqual(observer.seen, []);
});

test("a malformed frame ends the watch with a failed problem", async () => {
    const { streams, transport } = open();
    const observer = failures();
    transport.watch(liveQuery, {}, observer);
    await until(() => streams.length === 1);
    streams[0].push("data: {nope\n\n");
    await until(() => observer.seen.length === 1);
    assert.equal(observer.seen[0].state, "failed");
    assert.equal(observer.seen[0].failure.status, "failed");
});

test("a zero-or-one live frame is the bare row or null, the shape the server's Optional serializes to", async () => {
    const { streams, observer, transport } = open();
    transport.watch({ ...liveQuery, cardinality: "zeroOrOne", absent: () => "none" }, {}, observer);
    await until(() => streams.length === 1);
    streams[0].push("data: null\n\ndata: {\"id\":1}\n\n");
    await until(() => observer.values().length === 2);
    assert.deepEqual(observer.values(), ["value:\"none\"@0", "value:{\"id\":1}@0"]);
});
