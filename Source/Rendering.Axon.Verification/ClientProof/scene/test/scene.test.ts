import { afterEach, describe, it } from "node:test";
import assert from "node:assert/strict";
import { Mounted } from "./host.ts";
import { FakeTransport } from "./fakeTransport.ts";

describe("SceneElementView over a live query", () => {
    const mounted: Mounted[] = [];
    afterEach(() => { for (const view of mounted.splice(0)) view.unmount(); });

    function mount(transport: FakeTransport, owner: string): Mounted {
        const view = new Mounted(transport, owner);
        mounted.push(view);
        return view;
    }

    it("renders the results the watch delivers", () => {
        const transport = new FakeTransport();
        const view = mount(transport, "a");
        assert.deepEqual(view.items, []);
        assert.equal(transport.watches.length, 1);
        const [watch] = transport.watches;
        view.update(() => { watch?.observer.state("live", 0); transport.emit(watch!, ["x", "y"]); });
        assert.deepEqual(view.items, ["x", "y"]);
        assert.equal(view.state, "live");
    });

    it("never renders a value of the previous key after a key switch", () => {
        const transport = new FakeTransport();
        const view = mount(transport, "a");
        const first = transport.watches[0]!;
        view.update(() => transport.emit(first, ["a1"]));
        assert.deepEqual(view.items, ["a1"]);

        view.render("b");
        assert.equal(first.closed, true);
        assert.deepEqual(view.items, [], "the old key's value is gone as soon as the key changes");
        assert.equal(transport.watches.length, 2);

        const rendersBefore = view.renders;
        view.update(() => transport.emit(first, ["a2"]));
        assert.deepEqual(view.items, [], "a late value of the closed watch is ignored");
        assert.equal(view.renders, rendersBefore, "a late value of the closed watch causes no state update");
        view.update(() => transport.emit(transport.watches[1]!, ["b1"]));
        assert.deepEqual(view.items, ["b1"]);
        view.update(() => transport.emit(first, ["a3"]));
        assert.deepEqual(view.items, ["b1"]);
    });

    it("closes the watch on unmount and renders nothing afterwards", () => {
        const transport = new FakeTransport();
        const view = mount(transport, "a");
        const watch = transport.watches[0]!;
        view.update(() => transport.emit(watch, ["x"]));
        view.unmount();
        assert.equal(watch.closed, true);
        const rendersAfterUnmount = view.renders;
        assert.doesNotThrow(() => view.update(() => transport.emit(watch, ["late"])));
        assert.equal(view.renders, rendersAfterUnmount, "a value after unmount is never applied");
    });

    it("clears the rendered data on an identity change before any new value", () => {
        const transport = new FakeTransport();
        const view = mount(transport, "a");
        const watch = transport.watches[0]!;
        view.update(() => transport.emit(watch, ["secret"]));
        assert.deepEqual(view.items, ["secret"]);

        view.update(() => transport.changeIdentity());
        assert.deepEqual(view.items, [], "the previous identity's data is cleared at once");

        view.update(() => transport.emit(watch, ["stale"], 0));
        assert.deepEqual(view.items, [], "a value of the previous epoch is not rendered");

        view.update(() => transport.emit(watch, ["fresh"], 1));
        assert.deepEqual(view.items, ["fresh"]);
    });
});
