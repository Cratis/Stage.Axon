import { allWorkItems } from "../../src/work/items/overview.ts";
import { connect, eventually, expect, newItem, observe, ok, run } from "./support.ts";
import { seed } from "./seed.ts";

run(async () => {
    const { transport } = connect();
    const item = newItem();
    await seed(transport, item);

    const snapshot = await transport.snapshot(allWorkItems, {});
    ok(snapshot.status === "ok" && snapshot.value.filter(row => row.workItemId === item.id).length === 1, "the plain JSON GET to list the seeded row once");
    if (snapshot.status !== "ok") return;

    const watch = observe(transport, allWorkItems, {});
    try {
        const streamed = await eventually(() => watch.values().at(-1), "the stream to deliver the list");
        expect(streamed, snapshot.value, "the stream and the plain JSON GET to serve the same rows");
    } finally {
        watch.subscription.close();
    }
});
