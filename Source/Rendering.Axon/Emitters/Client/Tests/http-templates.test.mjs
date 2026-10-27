// The template rules the C# spec when_reading_the_templates asserts on rendered text, proven here on the template sources themselves.
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { templates } from "./support.mjs";

const names = ["transport", "http-transport", "sse"];
const source = (name) => readFileSync(new URL(name + ".ts", templates), "utf8");

test("no template uses dynamic imports, enums or namespaces", () => {
    for (const name of names) for (const word of ["import(", "enum ", "namespace "]) assert.equal(source(name).includes(word), false, name + " contains " + word);
});

test("every relative import ends in .ts", () => {
    for (const name of names) for (const line of source(name).split("\n").filter(candidate => candidate.includes("from \"./"))) assert.equal(line.endsWith(".ts\";"), true, name + ": " + line);
});

test("the transport seam stays neutral: no fetch, Response, URL or Authorization", () => {
    for (const word of ["fetch", "Response", "URL", "Authorization"]) assert.equal(source("transport").includes(word), false, word);
});

test("no template carries a license header", () => {
    for (const name of names) assert.equal(source(name).includes("Licensed under"), false, name);
});

test("credentials are omitted on every request site in the HTTP transport", () => {
    assert.equal(source("http-transport").split("credentials: \"omit\"").length - 1, 3);
    assert.equal(/credentials:\s*"(?!omit")/.test(source("http-transport")), false);
});
