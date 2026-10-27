import { workItemById } from "../../src/work/items/overview.ts";
import { connect, eventually, expect, java, newItem, observe, ok, pause, run } from "./support.ts";
import { seed } from "./seed.ts";

const burst = 100;

run(async () => {
    const { transport } = connect();
    const item = newItem({ title: "burst-0" });
    await seed(transport, item);
    const watch = observe(transport, workItemById, { workItemId: item.id });
    await eventually(() => watch.values().pop()?.title === "burst-0", "the initial snapshot");
    const before = watch.values().length;

    // Java starts the burst and replies at once; this process then stops reading (blocks its event loop) while the commits land.
    await java(`burst:${item.id}:${burst}`);
    pause(1500);
    await java("burst-done");

    const final = "burst-" + burst;
    await eventually(() => watch.values().pop()?.title === final, "the last frame to equal the final state");
    const sequence = watch.values().slice(before).map(value => Number(value?.title.slice("burst-".length)));
    ok(sequence.length <= burst, "no more frames than commits, got " + sequence.length);
    expect(sequence, [...sequence].sort((a, b) => a - b), "frames to arrive in commit order");
});
