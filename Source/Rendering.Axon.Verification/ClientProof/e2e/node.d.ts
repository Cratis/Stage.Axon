// The only Node surface the scenarios touch; declared here so the type-check needs no @types/node.
declare const process: {
    readonly argv: readonly string[];
    readonly stdin: { setEncoding(encoding: string): void; on(event: "data", listener: (chunk: string) => void): void };
    readonly stdout: { write(text: string): boolean };
    exit(code: number): never;
};
