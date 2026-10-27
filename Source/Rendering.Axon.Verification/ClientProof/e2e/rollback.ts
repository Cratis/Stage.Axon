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

    async function nextFrameIs(title: string, before: number): Promise<void> {
        await eventually(() => watch.values().length > before, "a frame after the sentinel");
        expect(watch.values()[before]?.title, title, "the first frame after the rollback");
    }

    // A refused command (409) must not publish anything: the next frame is the sentinel commit.
    expect((await transport.execute(createWorkItem, newItem({ name: item.name }))).status, "conflict", "the duplicate name");
    let before = watch.values().length;
    await java(`commit:${item.id}:s1`);
    await nextFrameIs("s1", before);

    // A rolled-back unit of work that announced a change must not publish either.
    await java(`rollback:${item.id}:ghost`);
    before = watch.values().length;
    await java(`commit:${item.id}:s2`);
    await nextFrameIs("s2", before);
});
