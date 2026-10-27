import { workItemById } from "../../src/work/items/overview.ts";
import { connect, eventually, expect, heldScheduler, java, newItem, observe, ok, run } from "./support.ts";
import { seed } from "./seed.ts";

run(async () => {
    const scheduler = heldScheduler();
    const { transport } = connect({ scheduler });
    const item = newItem({ title: "t0" });
    await seed(transport, item);
    const watch = observe(transport, workItemById, { workItemId: item.id });
    await eventually(() => watch.values().pop()?.title === "t0", "the initial snapshot");

    // The server expires the stream; the client reports reconnecting and its retry is held, so the commit lands inside the gap.
    await eventually(() => watch.states().includes("reconnecting"), "the expired stream to report reconnecting");
    ok(scheduler.held() === 1, "exactly one reconnect to be waiting");
    const before = watch.values().length;
    await java(`commit:${item.id}:t1`);
    scheduler.release();

    await eventually(() => watch.states().lastIndexOf("live") > watch.states().indexOf("reconnecting"), "the watch to go live again");
    await eventually(() => watch.values().length > before, "the fresh snapshot after reconnecting");
    expect(watch.values()[before]?.title, "t1", "the first frame after live is the snapshot with the commit made during the gap");
});
