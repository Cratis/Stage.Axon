// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

import { existsSync, readdirSync, readFileSync, realpathSync } from 'node:fs';
import { extname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

// These files define and exercise the patterns. Drop an entry if its file is renamed or removed.
const DEFAULT_ALLOW = ['.github/scripts/no-package-publish.mjs', '.github/scripts/no-package-publish.test.mjs'];
const excludedDirectories = new Set(['bin', 'obj', 'node_modules', 'packages', 'artifacts', 'target', '.ai-work', '.git']);
const msbuildExtensions = new Set(['.props', '.targets', '.csproj', '.proj', '.sln', '.slnx']);
const rules = [
    ['nuget-push', /\bnuget(?:\.exe)?\s+push\b/i],
    ['paket-push', /\bpaket(?:\.exe)?\s+push\b/i],
    ['package-file', /\.s?nupkg\b/i],
    ['registry-credentials', /NUGET_(?:API_KEY|AUTH_TOKEN)|--api-key\b|\bnuget\/login\b/i],
    ['release-asset', /softprops\/action-gh-release|actions\/upload-release-asset|\bgh\s+release\s+(?:create|upload)\b/i],
    ['registry-publish', /\b(?:npm|yarn|pnpm)\s+publish\b|\bmvnw?(?:\.cmd)?\b.*\bdeploy\b|\bgradlew?\b.*\bpublish\b/i]
];

function inspectFile(path, name, findings) {
    const lines = readFileSync(path, 'utf8').split(/\r?\n/);
    lines.forEach((line, index) => {
        for (const [rule, pattern] of rules) {
            if (pattern.test(line)) findings.push(`${name}:${index + 1} [${rule}] ${line.trim()}`);
        }
    });
}

function skipDirectory(name) {
    return excludedDirectories.has(name) || (name.startsWith('.') && name !== '.github');
}

function inspectEntry(entry, inGithub) {
    return entry.isFile() && (inGithub || msbuildExtensions.has(extname(entry.name).toLowerCase()));
}

function filesUnder(directory, inGithub = false) {
    const files = [];
    for (const entry of readdirSync(directory, { withFileTypes: true }).sort((a, b) => a.name.localeCompare(b.name))) {
        const path = join(directory, entry.name);
        if (entry.isDirectory()) {
            if (skipDirectory(entry.name)) continue;
            files.push(...filesUnder(path, inGithub || entry.name === '.github'));
        } else if (inspectEntry(entry, inGithub)) {
            files.push(path);
        }
    }
    return files;
}

function checkAllowlist(root, allow, findings) {
    for (const name of allow) {
        if (!existsSync(join(root, name))) findings.push(`${name}:1 [allowlist] allowlisted file does not exist`);
    }
}

function inspectFiles(root, allow, result) {
    for (const path of filesUnder(root)) {
        const name = relative(root, path).split('\\').join('/');
        if (/^\.github\/workflows\/.*\.ya?ml$/i.test(name)) result.workflows++;
        if (allow.includes(name)) continue;
        result.files++;
        inspectFile(path, name, result.findings);
    }
}

export function scan({ root, allow = DEFAULT_ALLOW }) {
    root = resolve(root);
    const result = { files: 0, workflows: 0, findings: [] };
    checkAllowlist(root, allow, result.findings);
    inspectFiles(root, allow, result);
    if (result.workflows === 0) result.findings.push('.github/workflows:1 [workflows] no workflows to inspect');
    return result;
}

export function main(args = process.argv.slice(2)) {
    try {
        let root = process.cwd();
        for (let index = 0; index < args.length; index++) {
            if (args[index] !== '--root' || !args[index + 1] || args[index + 1].startsWith('--')) throw new Error(`Unknown or incomplete argument: ${args[index]}`);
            root = resolve(args[++index]);
        }
        const result = scan({ root });
        const counts = `checked ${result.files} files (${result.workflows} workflows).`;
        if (result.findings.length > 0) {
            console.error(`Package publication check failed:\n${result.findings.join('\n')}\n${counts}`);
            return 1;
        }
        console.log(`No package publication: ${counts}`);
        return 0;
    } catch (error) {
        console.error(`Package publication check failed: ${error.message}`);
        return 1;
    }
}

if (process.argv[1] && existsSync(process.argv[1]) && realpathSync(process.argv[1]) === fileURLToPath(import.meta.url)) process.exitCode = main();
