import { createWorkItem } from "../../src/work/items/create.ts";
import type { CreateWorkItem } from "../../src/work/items/create.ts";
import { workItemById } from "../../src/work/items/overview.ts";
import type { Transport } from "../../src/transport.ts";
import { expect } from "./support.ts";

/** Creates the item through the generated client and waits until the read model serves it. */
export async function seed(transport: Transport, item: CreateWorkItem): Promise<void> {
    const created = await transport.execute(createWorkItem, item);
    expect(created.status, "ok", "create " + item.name);
    const deadline = Date.now() + 20_000;
    for (;;) {
        const outcome = await transport.snapshot(workItemById, { workItemId: item.id });
        if (outcome.status === "ok" && outcome.value !== null) return;
        if (Date.now() > deadline) throw new Error("The created item never became readable");
        await new Promise<void>(resolve => { setTimeout(resolve, 20); });
    }
}
