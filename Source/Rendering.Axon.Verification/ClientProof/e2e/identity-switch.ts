import { workItemById } from "../../src/work/items/overview.ts";
import { connect, eventually, expect, java, newItem, observe, ok, run } from "./support.ts";
import { seed } from "./seed.ts";

run(async () => {
    const editor = connect();
    const item = newItem({ title: "t0" });
    await seed(editor.transport, item);
    const watch = observe(editor.transport, workItemById, { workItemId: item.id });
    await eventually(() => watch.values().pop()?.title === "t0", "the initial snapshot");

    // Hold a snapshot inside the handler, then switch identity underneath it.
    await java("arm:after");
    const held = editor.transport.snapshot(workItemById, { workItemId: item.id });
    await java("held");
    const mark = watch.events.length;
    editor.switchTo("viewer");

    expect(await held, { status: "cancelled", reason: "identity" }, "the held snapshot");
    expect(editor.transport.epoch, 1, "the identity epoch");
    expect(watch.events[mark]?.kind, "cleared", "the first thing the watch reports after the switch");
    await eventually(() => watch.failures().length > 0, "the viewer watch to fail");
    expect(watch.failures(), [{ status: "forbidden" }], "the reopened watch under the viewer");

    await java("release");
    await java("active:0");

    // A second editor proves the server really did publish the commit; the old principal's watch must not have seen it.
    const other = connect();
    const witness = observe(other.transport, workItemById, { workItemId: item.id });
    await eventually(() => witness.values().pop()?.title === "t0", "the witness snapshot");
    await java(`commit:${item.id}:after-switch`);
    await eventually(() => witness.values().pop()?.title === "after-switch", "the witness to see the commit");
    ok(watch.events.slice(mark + 1).every(event => event.kind !== "value"), "no old-principal frame after the identity change");
});
