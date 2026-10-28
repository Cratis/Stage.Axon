import assert from "node:assert/strict";
import { test } from "node:test";
import { command, problem, scriptedFetch, transportFor } from "./support.mjs";

test("request JSON retains modelled keys rather than Java member names", async () => {
    const fetch = scriptedFetch(init => {
        assert.deepEqual(JSON.parse(init.body), { class: "arrived", "display-name": "unchanged" });
        return new Response(null, { status: 204 });
    });
    const outcome = await transportFor(fetch).execute(command, { class: "arrived", "display-name": "unchanged" });
    assert.deepEqual(outcome, { status: "ok", value: undefined });
    assert.equal(fetch.calls.length, 1);
});

test("validation failures retain modelled field paths", async () => {
    const errors = [
        { property: "class", message: "required" },
        { property: "address.class", message: "required" },
        { property: "addresses[0].class", message: "required" }
    ];
    const outcome = await transportFor(() => problem(400, "validation", { errors })).execute(command, { class: null });
    assert.deepEqual(outcome, { status: "validation", errors });
});
