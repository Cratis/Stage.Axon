import { createWorkItem } from "../../src/work/items/create.ts";
import { renameWorkItem } from "../../src/work/items/rename.ts";
import { echoClass } from "../../src/work/items/requestwirenames.ts";
import { workItemById, type WorkItems } from "../../src/work/items/overview.ts";
import { connect, eventually, expect, newItem, observe, ok, run } from "./support.ts";

run(async () => {
    const { transport } = connect();
    const echoed = await transport.execute(echoClass, { class: "wire-name-arrived" });
    expect(echoed, { status: "ok", value: { value: "wire-name-arrived" } }, "the modelled class key to reach the Java record's class_ component");

    const item = newItem({ state: "inProgress", priority: 5 });
    const watch = observe(transport, workItemById, { workItemId: item.id });

    await eventually(() => watch.states().includes("live"), "the watch to go live");
    expect(watch.values(), [null], "the initial snapshot of an item that does not exist yet");

    const created = await transport.execute(createWorkItem, item);
    expect(created, { status: "ok", value: { id: item.id, name: item.name } }, "the create command returns its record");

    const row = await eventually(() => watch.values().filter((value): value is WorkItems => value !== null).pop(), "the created item to arrive on the watch");
    expect(row, { workItemId: item.id, name: item.name, title: "first", state: "inProgress", priority: 5 }, "the watched row");
    ok(typeof row.state === "string" && typeof row.priority === "number", "the string enum to stay a string and the numbered enum a number");

    const renamed = await transport.execute(renameWorkItem, { id: item.id, title: "second" });
    expect(renamed.status, "ok", "the void rename");
    ok(renamed.status === "ok" && renamed.value === undefined, "a void command to carry no value");

    await eventually(() => watch.values().pop()?.title === "second", "the rename to reach the watch without a refresh");
    const snapshot = await transport.snapshot(workItemById, { workItemId: item.id });
    expect(snapshot.status === "ok" ? snapshot.value?.title : undefined, "second", "the snapshot after the rename");
});
