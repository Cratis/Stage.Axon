import type { BrowserDispatcherHost } from "@cratis/scene.react";
import type { CommandDescriptor, Outcome, Transport } from "../../../Rendering.Axon/Emitters/Client/Templates/transport.ts";

type CommandOutcome = Awaited<ReturnType<NonNullable<BrowserDispatcherHost["executeCommand"]>>>;

/** A command the Scene document can name, and the descriptor that runs it. */
export interface SceneCommand {
    readonly name: string;
    readonly command: CommandDescriptor<never, unknown>;
}

function describe(outcome: Exclude<Outcome<unknown>, { readonly status: "ok" } | { readonly status: "validation" }>): string {
    switch (outcome.status) {
        case "conflict": return "conflict" + (outcome.problem.detail === undefined ? "" : ": " + outcome.problem.detail);
        case "failed": return "failed" + (outcome.problem?.detail === undefined ? "" : ": " + outcome.problem.detail);
        case "cardinality": return "cardinality: " + outcome.count + " rows";
        case "unavailable": return "unavailable" + (outcome.retryAfterSeconds === undefined ? "" : ", retry after " + outcome.retryAfterSeconds + "s");
        case "cancelled": return "cancelled (" + outcome.reason + "): the server result is unknown";
        default: return outcome.status;
    }
}

/** Maps a transport outcome to a Scene command outcome: validation to `validationErrors`, everything else to `exceptionMessages`. */
export function toCommandOutcome(outcome: Outcome<unknown>): CommandOutcome {
    if (outcome.status === "ok") return { isSuccess: true, response: outcome.value };
    if (outcome.status === "validation") {
        return { isSuccess: false, validationErrors: outcome.errors.map(error => ({ member: error.property, message: error.message })) };
    }
    return { isSuccess: false, exceptionMessages: [describe(outcome)] };
}

/** Scene `executeCommand` over `Transport.execute`. Duplicate names throw here; an unknown name rejects the call. */
export function createSceneExecuteCommand(transport: Transport, commands: readonly SceneCommand[]): NonNullable<BrowserDispatcherHost["executeCommand"]> {
    const byName = new Map<string, CommandDescriptor<never, unknown>>();
    for (const entry of commands) {
        if (byName.has(entry.name)) throw new Error("Duplicate Scene command name '" + entry.name + "'");
        byName.set(entry.name, entry.command);
    }
    return async (name, args) => {
        const command = byName.get(name);
        if (command === undefined) throw new Error("Unknown Scene command '" + name + "'");
        return toCommandOutcome(await transport.execute(command, args as never));
    };
}
