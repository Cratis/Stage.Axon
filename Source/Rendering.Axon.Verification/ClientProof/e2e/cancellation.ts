import { workItemById } from "../../src/work/items/overview.ts";
import { connect, eventually, expect, java, newItem, observe, run } from "./support.ts";
import { seed } from "./seed.ts";

run(async () => {
    const { transport } = connect();
    const item = newItem({ title: "t0" });
    await seed(transport, item);
    const watch = observe(transport, workItemById, { workItemId: item.id });
    await eventually(() => watch.values().pop()?.title === "t0", "the initial snapshot");

    const reads = Number(await java("reads"));
    const marker = watch.events.length;
    watch.subscription.close();
    watch.subscription.close();
    await java("active:0");
    await java(`commit:${item.id}:later`);

    expect(watch.events.slice(marker).filter(event => event.kind === "value").length, 0, "the value frames delivered to the closed watch");

    // One snapshot is exactly one read: the closed watch did not read for the commit.
    const snapshot = await transport.snapshot(workItemById, { workItemId: item.id });
    expect(snapshot.status === "ok" ? snapshot.value?.title : undefined, "later", "the snapshot after closing");
    expect(Number(await java("reads")), reads + 1, "the handler read count after closing");
});
