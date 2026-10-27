// The proof pins no @types/react-dom and jsdom ships no types; these cover only the surface the host uses.
declare module "react-dom/client" {
    import type { ReactNode } from "react";
    export interface Root {
        render(children: ReactNode): void;
        unmount(): void;
    }
    export function createRoot(container: Element): Root;
}

declare module "jsdom" {
    export class JSDOM {
        constructor(html?: string, options?: { readonly url?: string });
        readonly window: Window & typeof globalThis;
    }
}
