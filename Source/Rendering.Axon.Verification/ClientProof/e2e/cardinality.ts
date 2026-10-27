import { workItemById, workItemByName } from "../../src/work/items/overview.ts";
import { connect, expect, java, newItem, ok, run } from "./support.ts";
import { seed } from "./seed.ts";

run(async () => {
    const { transport } = connect();
    expect(await transport.snapshot(workItemById, { workItemId: crypto.randomUUID() }), { status: "ok", value: null }, "an absent zero-or-one row");

    const item = newItem();
    await seed(transport, item);
    const byName = await transport.snapshot(workItemByName, { name: item.name });
    ok(byName.status === "ok" && byName.value.length === 1 && byName.value[0]?.workItemId === item.id, "the many query to return the one matching row");

    await java("duplicate:" + item.id);
    expect(await transport.snapshot(workItemById, { workItemId: item.id }), { status: "cardinality", count: 2 }, "two rows for a zero-or-one query");
});
