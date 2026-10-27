/** One server-sent frame: either a data payload, or a comment such as the server's heartbeat. */
export type SseFrame =
    | { readonly kind: "data"; readonly data: string }
    | { readonly kind: "comment" };

/** Thrown when a frame grows beyond the allowed size. */
export class FrameTooLarge extends Error {
    constructor(limit: number) {
        super("A server-sent frame exceeded " + limit + " characters");
        this.name = "FrameTooLarge";
    }
}

const lineEnd = /\r\n|\n|\r/;

function takeLine(buffer: string, final: boolean): { line: string; rest: string } | undefined {
    const match = lineEnd.exec(buffer);
    if (match === null) return undefined;
    // A lone carriage return at the end of a chunk may be the first half of CRLF; wait for the next chunk.
    if (match[0] === "\r" && match.index === buffer.length - 1 && !final) return undefined;
    return { line: buffer.slice(0, match.index), rest: buffer.slice(match.index + match[0].length) };
}

/**
 * Reads server-sent frames from a response body. Comment lines are reported, `id`, `event` and `retry` fields are ignored,
 * and an event is only delivered when its terminating blank line arrives.
 */
export async function* readFrames(body: ReadableStream<Uint8Array>, maxFrameChars: number): AsyncGenerator<SseFrame, void, undefined> {
    const reader = body.getReader();
    const decoder = new TextDecoder();
    let buffer = "";
    let data: string[] = [];
    let size = 0;
    let final = false;
    try {
        while (!final) {
            const chunk = await reader.read();
            if (chunk.done) {
                final = true;
                buffer += decoder.decode();
            } else {
                buffer += decoder.decode(chunk.value, { stream: true });
            }

            for (let taken = takeLine(buffer, final); taken !== undefined; taken = takeLine(buffer, final)) {
                buffer = taken.rest;
                if (taken.line === "") {
                    if (data.length > 0) yield { kind: "data", data: data.join("\n") };
                    data = [];
                    size = 0;
                } else if (taken.line.startsWith(":")) {
                    yield { kind: "comment" };
                } else {
                    const colon = taken.line.indexOf(":");
                    const field = colon < 0 ? taken.line : taken.line.slice(0, colon);
                    if (field === "data") {
                        const raw = colon < 0 ? "" : taken.line.slice(colon + 1);
                        const text = raw.startsWith(" ") ? raw.slice(1) : raw;
                        size += text.length;
                        if (size > maxFrameChars) throw new FrameTooLarge(maxFrameChars);
                        data.push(text);
                    }
                }
            }

            if (buffer.length + size > maxFrameChars) throw new FrameTooLarge(maxFrameChars);
        }
    } finally {
        await reader.cancel().catch(() => undefined);
    }
}
