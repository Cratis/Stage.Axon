/** How many rows a query returns. */
export type Cardinality = "many" | "one" | "zeroOrOne";

/** Turns an untyped wire value into a typed one, or throws a {@link DecodeError}. */
export type Decoder<T> = (value: unknown, path: string) => T;

/** Thrown by a decoder when the wire value does not have the expected shape. */
export class DecodeError extends Error {
    readonly path: string;

    constructor(path: string, message: string) {
        super(path + ": " + message);
        this.name = "DecodeError";
        this.path = path;
    }
}

export function decodeUnknown(value: unknown): unknown {
    return value;
}

export function decodeObject(value: unknown, path: string): Record<string, unknown> {
    if (typeof value === "object" && value !== null && !Array.isArray(value)) return Object.fromEntries(Object.entries(value));
    throw new DecodeError(path, "expected an object");
}

export function decodeString(value: unknown, path: string): string {
    if (typeof value === "string") return value;
    throw new DecodeError(path, "expected a string");
}

export function decodeBoolean(value: unknown, path: string): boolean {
    if (typeof value === "boolean") return value;
    throw new DecodeError(path, "expected a boolean");
}

export function decodeNumber(value: unknown, path: string): number {
    if (typeof value === "number" && Number.isFinite(value)) return value;
    throw new DecodeError(path, "expected a finite number");
}

export function decodeSafeInteger(value: unknown, path: string): number {
    if (typeof value === "number" && Number.isSafeInteger(value)) return value;
    throw new DecodeError(path, "expected a whole number within the safe integer range");
}

export function decodeOneOf<T extends string | number>(value: unknown, path: string, allowed: readonly T[]): T {
    for (const candidate of allowed) if (candidate === value) return candidate;
    throw new DecodeError(path, "expected one of " + allowed.join(", "));
}

export function arrayOf<T>(item: Decoder<T>): Decoder<T[]> {
    return (value, path) => {
        if (!Array.isArray(value)) throw new DecodeError(path, "expected an array");
        return value.map((entry: unknown, index) => item(entry, path + "[" + index + "]"));
    };
}

export function nullable<T>(item: Decoder<T>): Decoder<T | null> {
    return (value, path) => (value === null || value === undefined ? null : item(value, path));
}

/** A command the server accepts. `C` is the request body and `R` the result; `void` means the server returns no body. */
export interface CommandDescriptor<C, R> {
    readonly id: string;
    /** Type-only marker that ties the descriptor to its request body type; it is never set. */
    readonly request?: C;
    readonly decode?: Decoder<R>;
}

/** A query the server answers. `K` is the key and `R` the whole result: a list, one row, or a row or `null`. */
export interface QueryDescriptor<K, R> {
    readonly id: string;
    /** Type-only marker that ties the descriptor to its key type; it is never set. */
    readonly key?: K;
    readonly cardinality: Cardinality;
    /** Whether the server also offers the query as a live stream. */
    readonly live: boolean;
    readonly decode: Decoder<R>;
    /** The result when a zero-or-one query finds nothing. */
    readonly absent?: () => R;
}

export interface ValidationFailure {
    readonly property: string;
    readonly message: string;
}

/** The problem details the server answered with. */
export interface Problem {
    readonly type: string | undefined;
    readonly status: number;
    readonly detail: string | undefined;
}

/**
 * The result of a request. Every server answer maps to a status; nothing throws.
 * A `cancelled` command has an unknown server result: it may or may not have run.
 */
export type Outcome<T> =
    | { readonly status: "ok"; readonly value: T }
    | { readonly status: "unauthenticated" }
    | { readonly status: "forbidden" }
    | { readonly status: "validation"; readonly errors: readonly ValidationFailure[] }
    | { readonly status: "conflict"; readonly problem: Problem }
    | { readonly status: "notFound" }
    | { readonly status: "cardinality"; readonly count: number }
    | { readonly status: "unavailable"; readonly retryAfterSeconds: number | undefined }
    | { readonly status: "cancelled"; readonly reason: "aborted" | "identity" }
    | { readonly status: "failed"; readonly problem: Problem | undefined };

export type LiveState = "connecting" | "live" | "reconnecting" | "closed" | "failed";

/**
 * Why a watch reports `failed` or `reconnecting`, in the same terms as a snapshot or command outcome.
 * A terminal `failed` carries the reason: `unauthenticated` for 401, `forbidden` for 403, otherwise the problem kind.
 * `reconnecting` carries `unavailable` when the server sent `Retry-After`, for example when it is at its live-stream capacity.
 */
export type LiveFailure = Exclude<Outcome<never>, { readonly status: "ok" } | { readonly status: "cancelled" }>;

/** Receives live results. The epoch is the identity epoch the callback belongs to; callbacks from an older epoch are never made. */
export interface WatchObserver<T> {
    value(value: T, epoch: number): void;
    state(state: LiveState, epoch: number, failure?: LiveFailure): void;
    /** The identity changed: the previous value no longer applies. */
    cleared(epoch: number): void;
}

export interface Subscription {
    /** Stops the watch. Closing more than once has no further effect. */
    close(): void;
}

/** The transport seam the generated descriptors run over. */
export interface Transport {
    /** Counts identity changes; starts at 0. */
    readonly epoch: number;
    /**
     * Declares that the caller's identity changed: in-flight requests resolve as `cancelled("identity")`,
     * and every watch is cleared and reopened under the new identity.
     */
    changeIdentity(): void;
    execute<C, R>(command: CommandDescriptor<C, R>, body: C, signal?: AbortSignal): Promise<Outcome<R>>;
    snapshot<K extends object, R>(query: QueryDescriptor<K, R>, key: K, signal?: AbortSignal): Promise<Outcome<R>>;
    watch<K extends object, R>(query: QueryDescriptor<K, R>, key: K, observer: WatchObserver<R>, signal?: AbortSignal): Subscription;
}
