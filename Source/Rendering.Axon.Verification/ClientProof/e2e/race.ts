import { workItemById } from "../../src/work/items/overview.ts";
import { connect, eventually, expect, java, newItem, observe, ok, run } from "./support.ts";
import { seed } from "./seed.ts";

run(async () => {
    const { transport } = connect();
    // "after": the initial read happens before the commit and is delivered after it. "before": the commit lands before the read.
    for (const interleaving of ["after", "before"]) {
        const item = newItem({ title: "v0" });
        await seed(transport, item);
        await java("arm:" + interleaving);
        const watch = observe(transport, workItemById, { workItemId: item.id });
        const committed = interleaving + "-v1";
        await java(`release-commit:${item.id}:${committed}`);
        await eventually(() => watch.values().pop()?.title === committed, interleaving + ": the watch to end on the committed value");

        const sentinel = interleaving + "-v2";
        await java(`commit:${item.id}:${sentinel}`);
        await eventually(() => watch.values().pop()?.title === sentinel, interleaving + ": the sentinel");
        const titles = watch.values().map(value => value?.title);
        const firstCommitted = titles.indexOf(committed);
        ok(firstCommitted >= 0, interleaving + ": the committed value to be delivered");
        expect(titles.slice(firstCommitted).filter(title => title !== committed && title !== sentinel), [], interleaving + ": no stale value after the committed one");
        watch.subscription.close();
    }
});
