import { createWorkItem } from "../../src/work/items/create.ts";
import { workItemById } from "../../src/work/items/overview.ts";
import { connect, expect, newItem, run } from "./support.ts";
import { seed } from "./seed.ts";

run(async () => {
    const { transport } = connect();
    const first = newItem({ title: "original" });
    await seed(transport, first);

    const duplicate = newItem({ name: first.name, title: "duplicate" });
    expect((await transport.execute(createWorkItem, duplicate)).status, "conflict", "a duplicate name");

    const original = await transport.snapshot(workItemById, { workItemId: first.id });
    expect(original.status === "ok" ? original.value?.title : undefined, "original", "the original item stays unchanged");
    expect(await transport.snapshot(workItemById, { workItemId: duplicate.id }), { status: "ok", value: null }, "the refused item must not exist");
});
