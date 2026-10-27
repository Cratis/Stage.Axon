import { useEffect, useState } from "react";
import type { LiveFailure, LiveState, QueryDescriptor, Transport } from "../../../Rendering.Axon/Emitters/Client/Templates/transport.ts";

export interface LiveQueryResults {
    /** Shaped for Scene's `queryResults`; empty until the first value of the current key and identity arrives. */
    readonly queryResults: Record<string, unknown>;
    readonly state: LiveState;
    readonly failure: LiveFailure | undefined;
}

interface Held {
    readonly signature: string;
    readonly value: { readonly current: unknown } | undefined;
    readonly state: LiveState;
    readonly failure: LiveFailure | undefined;
}

/**
 * Watches one live query and exposes it as Scene `queryResults` under `name`.
 * The watch closes on unmount and on a key change. Values of a superseded watch, or of an older identity epoch, are never shown.
 */
export function useLiveQueryResults<K extends object, R>(transport: Transport, query: QueryDescriptor<K, R>, key: K, name: string): LiveQueryResults {
    const signature = query.id + "\u0000" + JSON.stringify(key);
    const [held, setHeld] = useState<Held>({ signature, value: undefined, state: "connecting", failure: undefined });

    useEffect(() => {
        let open = true;
        let latest = transport.epoch;
        setHeld({ signature, value: undefined, state: "connecting", failure: undefined });
        const subscription = transport.watch(query, key, {
            value(value, epoch) {
                if (!open || epoch < latest) return;
                latest = epoch;
                setHeld(previous => ({ ...previous, signature, value: { current: value } }));
            },
            state(state, epoch, failure) {
                if (!open || epoch < latest) return;
                latest = epoch;
                setHeld(previous => ({ ...previous, signature, state, failure }));
            },
            cleared(epoch) {
                if (!open || epoch < latest) return;
                latest = epoch;
                setHeld(previous => ({ ...previous, signature, value: undefined }));
            },
        });
        return () => {
            open = false;
            subscription.close();
        };
        // The signature stands for the query and key; the transport is fixed for the life of the host.
    }, [transport, signature]);

    // A render for a new key must not show the previous key's value while the effect has yet to run.
    const current = held.signature === signature ? held : undefined;
    return {
        queryResults: current?.value === undefined ? {} : { [name]: current.value.current },
        state: current?.state ?? "connecting",
        failure: current?.failure,
    };
}
