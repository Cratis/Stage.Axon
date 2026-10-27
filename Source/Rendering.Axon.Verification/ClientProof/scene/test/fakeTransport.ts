import type { CommandDescriptor, Outcome, QueryDescriptor, Subscription, Transport, WatchObserver } from "../../../../Rendering.Axon/Emitters/Client/Templates/transport.ts";

export interface FakeWatch {
    readonly queryId: string;
    readonly key: object;
    readonly observer: WatchObserver<never>;
    closed: boolean;
}

/** An in-memory transport: the test decides what every request answers and when each watch emits. */
export class FakeTransport implements Transport {
    readonly watches: FakeWatch[] = [];
    readonly executed: { readonly id: string; readonly body: unknown }[] = [];
    #epoch = 0;
    #answer: Outcome<unknown> = { status: "ok", value: undefined };

    get epoch(): number {
        return this.#epoch;
    }

    answerWith(outcome: Outcome<unknown>): void {
        this.#answer = outcome;
    }

    changeIdentity(): void {
        this.#epoch++;
        for (const watch of this.watches) if (!watch.closed) watch.observer.cleared(this.#epoch);
    }

    execute<C, R>(command: CommandDescriptor<C, R>, body: C): Promise<Outcome<R>> {
        this.executed.push({ id: command.id, body });
        return Promise.resolve(this.#answer as Outcome<R>);
    }

    snapshot<K extends object, R>(): Promise<Outcome<R>> {
        return Promise.resolve({ status: "notFound" });
    }

    watch<K extends object, R>(query: QueryDescriptor<K, R>, key: K, observer: WatchObserver<R>): Subscription {
        const watch: FakeWatch = { queryId: query.id, key, observer: observer as WatchObserver<never>, closed: false };
        this.watches.push(watch);
        return { close: () => { watch.closed = true; } };
    }

    /** Emits a value on a watch at an explicit epoch, whether or not the watch is still open: a late delivery is what the host must ignore. */
    emit(watch: FakeWatch, value: unknown, epoch: number = this.#epoch): void {
        (watch.observer as WatchObserver<unknown>).value(value, epoch);
    }
}

/** Resolves with the promise, or rejects once the deadline passes. The timer is only a deadline and is always cleared. */
export async function within<T>(promise: Promise<T>, milliseconds: number, label: string): Promise<T> {
    let timer: ReturnType<typeof setTimeout> | undefined;
    const deadline = new Promise<never>((_, reject) => { timer = setTimeout(() => reject(new Error("Deadline exceeded: " + label)), milliseconds); });
    try {
        return await Promise.race([promise, deadline]);
    } finally {
        clearTimeout(timer);
    }
}
