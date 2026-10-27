import { createWorkItem } from "../../src/work/items/create.ts";
import { connect, expect, newItem, ok, run } from "./support.ts";

run(async () => {
    const { transport } = connect();
    const outcome = await transport.execute(createWorkItem, { ...newItem(), title: null as unknown as string });
    expect(outcome.status, "validation", "a null title");
    ok(outcome.status === "validation" && outcome.errors[0]?.property === "title", "the first validation error to name the title property");
});
