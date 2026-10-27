import { workItemById } from "../../src/work/items/overview.ts";
import { connect, eventually, expect, heldScheduler, observe, ok, run } from "./support.ts";

run(async () => {
    const scheduler = heldScheduler();
    const { transport } = connect({ scheduler });
    const first = observe(transport, workItemById, { workItemId: crypto.randomUUID() });
    const second = observe(transport, workItemById, { workItemId: crypto.randomUUID() });
    await eventually(() => first.states().includes("live") && second.states().includes("live"), "two watches to go live");

    const third = observe(transport, workItemById, { workItemId: crypto.randomUUID() });
    await eventually(() => third.failures().length > 0, "the third watch to be refused");
    expect(third.failures(), [{ status: "unavailable", retryAfterSeconds: 5 }], "the capacity refusal");
    expect(third.states().filter(state => state !== "connecting"), ["reconnecting"], "the third watch only waits to reconnect");
    ok(!third.states().includes("live"), "the third watch never to go live");
});
