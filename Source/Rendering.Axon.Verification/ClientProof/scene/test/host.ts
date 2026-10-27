import "./dom.ts";
import { createElement, act, type ReactNode } from "react";
import { createRoot, type Root } from "react-dom/client";
import { SceneElementView, useRenderBindingScope, type ComponentRegistry, type SceneElementViewProps } from "@cratis/scene.react";
import type { QueryDescriptor, Transport } from "../../../../Rendering.Axon/Emitters/Client/Templates/transport.ts";
import { useLiveQueryResults } from "../useLiveQueryResults.ts";

export const rows: QueryDescriptor<{ readonly owner: string }, readonly string[]> = {
    id: "Rows",
    cardinality: "many",
    live: true,
    decode: value => value as string[],
};

const element = {
    id: "results",
    name: "results",
    componentName: "Results",
    slots: {},
    properties: {},
} as unknown as SceneElementViewProps["element"];

function Results(): ReactNode {
    const items = useRenderBindingScope().queryResults?.["items"];
    return createElement("ul", null, Array.isArray(items) ? items.map(item => createElement("li", { key: String(item) }, String(item))) : null);
}

const registry: ComponentRegistry = { Results };

function Host(props: { readonly transport: Transport; readonly owner: string; readonly onRender: () => void }): ReactNode {
    props.onRender();
    const { queryResults, state } = useLiveQueryResults(props.transport, rows, { owner: props.owner }, "items");
    return createElement("div", { "data-state": state }, createElement(SceneElementView, { element, registry, queryResults }));
}

export class Mounted {
    readonly container = document.createElement("div");
    readonly #root: Root;
    #unmounted = false;

    /** How many times the host component rendered; a stale value that is applied makes this grow. */
    renders = 0;

    readonly transport: Transport;

    constructor(transport: Transport, owner: string) {
        this.transport = transport;
        document.body.append(this.container);
        this.#root = createRoot(this.container);
        this.render(owner);
    }

    render(owner: string): void {
        act(() => this.#root.render(createElement(Host, { transport: this.transport, owner, onRender: () => { this.renders++; } })));
    }

    /** Runs a change that makes React update, such as a transport callback. */
    update(change: () => void): void {
        act(change);
    }

    unmount(): void {
        if (this.#unmounted) return;
        this.#unmounted = true;
        act(() => this.#root.unmount());
        this.container.remove();
    }

    get items(): string[] {
        return [...this.container.querySelectorAll("li")].map(item => item.textContent ?? "");
    }

    get state(): string | null {
        return this.container.querySelector("[data-state]")?.getAttribute("data-state") ?? null;
    }
}
