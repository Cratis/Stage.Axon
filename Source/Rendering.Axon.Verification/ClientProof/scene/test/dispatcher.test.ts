import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { createSceneExecuteCommand, toCommandOutcome } from "../sceneDispatcher.ts";
import type { CommandDescriptor, Outcome } from "../../../../Rendering.Axon/Emitters/Client/Templates/transport.ts";
import { FakeTransport, within } from "./fakeTransport.ts";

const rename: CommandDescriptor<never, unknown> = { id: "Rename" };
const problem = { type: undefined, status: 409, detail: "taken" };

describe("Scene command outcome mapping", () => {
    it("maps ok to a success carrying the response", () => {
        assert.deepEqual(toCommandOutcome({ status: "ok", value: 7 }), { isSuccess: true, response: 7 });
    });

    it("maps validation to validationErrors", () => {
        const outcome = toCommandOutcome({ status: "validation", errors: [{ property: "name", message: "required" }] });
        assert.deepEqual(outcome, { isSuccess: false, validationErrors: [{ member: "name", message: "required" }] });
    });

    const others: readonly [Outcome<unknown>, string][] = [
        [{ status: "unauthenticated" }, "unauthenticated"],
        [{ status: "forbidden" }, "forbidden"],
        [{ status: "conflict", problem }, "conflict: taken"],
        [{ status: "notFound" }, "notFound"],
        [{ status: "cardinality", count: 2 }, "cardinality: 2 rows"],
        [{ status: "unavailable", retryAfterSeconds: 3 }, "unavailable, retry after 3s"],
        [{ status: "cancelled", reason: "identity" }, "cancelled (identity): the server result is unknown"],
        [{ status: "cancelled", reason: "aborted" }, "cancelled (aborted): the server result is unknown"],
        [{ status: "failed", problem: undefined }, "failed"],
    ];
    for (const [outcome, message] of others) {
        it("maps " + outcome.status + " to exceptionMessages", () => {
            const mapped = toCommandOutcome(outcome);
            assert.deepEqual(mapped, { isSuccess: false, exceptionMessages: [message] });
            assert.equal("validationErrors" in mapped, false);
        });
    }
});

describe("Scene executeCommand", () => {
    it("runs the named command over the transport", async () => {
        const transport = new FakeTransport();
        transport.answerWith({ status: "ok", value: "done" });
        const execute = createSceneExecuteCommand(transport, [{ name: "rename", command: rename }]);
        const outcome = await within(execute("rename", { name: "n" }), 1000, "execute");
        assert.deepEqual(outcome, { isSuccess: true, response: "done" });
        assert.deepEqual(transport.executed, [{ id: "Rename", body: { name: "n" } }]);
    });

    it("rejects an unknown command name without calling the transport", async () => {
        const transport = new FakeTransport();
        const execute = createSceneExecuteCommand(transport, [{ name: "rename", command: rename }]);
        await assert.rejects(within(execute("missing", {}), 1000, "unknown"), /Unknown Scene command 'missing'/);
        assert.equal(transport.executed.length, 0);
    });

    it("throws on a duplicate command name", () => {
        assert.throws(
            () => createSceneExecuteCommand(new FakeTransport(), [{ name: "rename", command: rename }, { name: "rename", command: rename }]),
            /Duplicate Scene command name 'rename'/);
    });
});
