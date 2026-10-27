import { createWorkItem } from "../../src/work/items/create.ts";
import { workItemById } from "../../src/work/items/overview.ts";
import { connect, eventually, expect, java, newItem, observe, ok, run, type Identity } from "./support.ts";

run(async () => {
    const denied: readonly (readonly [Identity, "unauthenticated" | "forbidden"])[] = [["anonymous", "unauthenticated"], ["viewer", "forbidden"]];
    for (const [identity, status] of denied) {
        const { transport } = connect({ identity });
        const item = newItem();
        expect(await transport.execute(createWorkItem, item), { status }, identity + " command");
        expect(await transport.snapshot(workItemById, { workItemId: item.id }), { status }, identity + " snapshot");
        const watch = observe(transport, workItemById, { workItemId: item.id });
        await eventually(() => watch.failures().length > 0, identity + " watch to fail");
        expect(watch.failures(), [{ status }], identity + " watch failure");
        ok(watch.states().includes("failed") && !watch.states().includes("live"), identity + " watch to end failed without going live");
    }

    const { transport } = connect({ headers: { "X-User": "mallory" } });
    expect((await transport.execute(createWorkItem, newItem())).status, "ok", "the editor command with a claimed identity header");
    const callers = (await java("callers")).split(",");
    ok(callers.includes("mallory>editor"), "the server to have run the command as editor despite X-User: mallory, saw " + callers.join(","));
});
