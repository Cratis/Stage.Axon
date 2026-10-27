import { createWorkItem } from "../../src/work/items/create.ts";
import { workItemById } from "../../src/work/items/overview.ts";
import { connect, eventually, expect, java, newItem, observe, run } from "./support.ts";
import { seed } from "./seed.ts";

run(async () => {
    const { transport } = connect();
    const item = newItem({ title: "t0" });
    await seed(transport, item);
    const watch = observe(transport, workItemById, { workItemId: item.id });
    await eventually(() => watch.values().pop()?.title === "t0", "the initial snapshot");

    // Everything the watch delivers from ${marker} on must be exactly the sentinel: the marker is taken before the
    // action under test, so a ghost frame published before the action completes is inside the slice.
    async function onlyFrameIs(title: string, marker: number, what: string): Promise<void> {
        await eventually(() => watch.values().slice(marker).some(value => value?.title === title), "the sentinel after the " + what);
        expect(watch.values().slice(marker).map(value => value?.title), [title], "the value frames after the " + what);
    }

    // A refused command (409) must not publish anything: the only frame is the sentinel commit.
    let marker = watch.values().length;
    expect((await transport.execute(createWorkItem, newItem({ name: item.name }))).status, "conflict", "the duplicate name");
    await java(`commit:${item.id}:s1`);
    await onlyFrameIs("s1", marker, "rejected command");

    // A rolled-back unit of work that announced a change must not publish either.
    marker = watch.values().length;
    await java(`rollback:${item.id}:ghost`);
    await java(`commit:${item.id}:s2`);
    await onlyFrameIs("s2", marker, "rollback");
});
