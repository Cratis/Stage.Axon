// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

import { existsSync, readdirSync, readFileSync, realpathSync } from 'node:fs';
import { basename, extname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

// These files define and exercise the patterns. Drop an entry if its file is renamed or removed.
const DEFAULT_ALLOW = ['.github/scripts/package-allowlist.mjs', '.github/scripts/package-allowlist.test.mjs'];
export const PACKAGES = [{ project: 'Source/Rendering.Axon/Rendering.Axon.csproj', id: 'Cratis.Stage.Rendering.Axon' }];
export const MAVEN_PACKAGES = [{ pom: 'Source/Runtime/pom.xml', groupId: 'io.cratis', artifactId: 'stage-axon-runtime' }];
export const PUBLISH_WORKFLOW = '.github/workflows/publish.yml';
const publishRules = new Set(['nuget-push', 'package-file', 'registry-credentials']);
const excludedDirectories = new Set(['bin', 'obj', 'node_modules', 'packages', 'artifacts', 'target', '.ai-work', '.git']);
const mavenPublication = /central-publishing-maven-plugin|<distributionManagement\b|maven-deploy-plugin|nexus-staging/i;
const mavenDeploy = /\bmvnw?(?:\.cmd)?\b.*\bdeploy\b/i;
const msbuildExtensions = new Set(['.props', '.targets', '.csproj', '.proj', '.sln', '.slnx']);
const rules = [
    ['nuget-push', /\bnuget(?:\.exe)?\s+push\b/i],
    ['paket-push', /\bpaket(?:\.exe)?\s+push\b/i],
    ['package-file', /\.s?nupkg\b/i],
    ['registry-credentials', /NUGET_(?:API_KEY|AUTH_TOKEN)|MAVEN_CENTRAL_(?:USERNAME|PASSWORD)|MAVEN_GPG_(?:PRIVATE_KEY|PASSPHRASE|KEY)|--api-key\b|\bnuget\/login\b/i],
    ['release-asset', /softprops\/action-gh-release|actions\/upload-release-asset|\bgh\s+release\s+(?:create|upload)\b/i],
    ['registry-publish', /\b(?:npm|yarn|pnpm)\s+publish\b|\bmvnw?(?:\.cmd)?\b.*\bdeploy\b|\bgradlew?\b.*\bpublish\b/i]
];

function hasPack(line, project) {
    return new RegExp(`\\bdotnet\\s+pack\\s+["']?${escapeRegex(project)}(?:["']|\\s|$)`, 'i').test(line);
}

function escapeRegex(value) {
    return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

const approvedDeploy = /^(?:\.\/)?(?:[\w.\/-]*\/)?mvnw?(?:\.cmd)?((?:\s+(?:-B|-f\s+\S+|-D[\w.]+=[\w.${}"'-]*))*)\s+deploy$/i;

// A shell command line is several commands; only a command that is exactly the approved deploy is exempt.
function commands(line) {
    return line.split(/&&|\|\||[;|&()`]/).map(part => part.replace(/^\s*(?:-\s+)?(?:run:\s*)?/, '').trim());
}

function isApprovedDeploy(command, mavenPackages) {
    const match = approvedDeploy.exec(command);
    if (!match) return false;
    const targets = [...match[1].matchAll(/-f\s+(\S+)/g)].map(target => target[1].replace(/^\.\//, ''));
    return targets.length === 1 && mavenPackages.some(pkg => pkg.pom === targets[0]);
}

function withoutApprovedDeploys(line, mavenPackages) {
    return commands(line).filter(command => !isApprovedDeploy(command, mavenPackages)).join(' ; ');
}

// Joins shell line continuations so a command split across lines is judged as one command.
function logicalLines(text) {
    const result = [];
    let pending = null;
    text.split(/\r?\n/).forEach((line, index) => {
        const start = pending ?? { index, text: '' };
        const joined = start.text + line.replace(/\\$/, ' ');
        if (/\\$/.test(line)) pending = { index: start.index, text: joined };
        else { result.push({ index: start.index, text: joined }); pending = null; }
    });
    if (pending) result.push({ index: pending.index, text: pending.text });
    return result;
}

function inspectFile(path, name, findings, packages, publishWorkflow, mavenPackages) {
    const text = readFileSync(path, 'utf8');
    logicalLines(text).forEach(({ index, text: full }) => {
        const line = name === publishWorkflow ? withoutApprovedDeploys(full, mavenPackages) : full;
        for (const [rule, pattern] of rules) {
            if (name === publishWorkflow && publishRules.has(rule)) continue;
            if (pattern.test(line)) findings.push(`${name}:${index + 1} [${rule}] ${full.trim()}`);
        }
        if (name === publishWorkflow && mavenDeploy.test(line)) {
            findings.push(`${name}:${index + 1} [maven-scope] only the approved allowlisted Maven deploy command may deploy: ${full.trim()}`);
        }
        if (/\bdotnet\s+pack\b/i.test(line) && !packages.some(pkg => hasPack(line, pkg.project))) {
            findings.push(`${name}:${index + 1} [pack-scope] dotnet pack must target an allowlisted project`);
        }
    });
    if (msbuildExtensions.has(extname(name).toLowerCase())) inspectPackable(text, name, findings, packages);
    if (basename(name) === 'pom.xml' && !mavenPackages.some(pkg => pkg.pom === name) && mavenPublication.test(text)) {
        findings.push(`${name}:1 [maven-publication] only an allowlisted pom may configure Maven publication`);
    }
}

function inspectPackable(text, name, findings, packages) {
    const allowed = packages.some(pkg => pkg.project === name);
    for (const match of text.matchAll(/<(IsPackable|GeneratePackageOnBuild)\b[^>]*>([\s\S]*?)<\/\1>/gi)) {
        const value = match[2].trim().toLowerCase();
        const invalid = match[1].toLowerCase() === 'ispackable' ? !allowed && value !== 'false' : value === 'true';
        if (invalid) {
            const line = text.slice(0, match.index).split('\n').length;
            findings.push(`${name}:${line} [packable] ${match[0]}`);
        }
    }
}

function skipDirectory(name) {
    return excludedDirectories.has(name) || (name.startsWith('.') && name !== '.github');
}

function inspectEntry(entry, inGithub) {
    return entry.isFile() && (inGithub || entry.name === 'pom.xml' || msbuildExtensions.has(extname(entry.name).toLowerCase()));
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

function inspectFiles(root, allow, result, packages, publishWorkflow, mavenPackages) {
    for (const path of filesUnder(root)) {
        const name = relative(root, path).split('\\').join('/');
        if (/^\.github\/workflows\/.*\.ya?ml$/i.test(name)) result.workflows++;
        if (allow.includes(name)) continue;
        result.files++;
        inspectFile(path, name, result.findings, packages, publishWorkflow, mavenPackages);
    }
}

function checkProjects(root, packages, findings) {
    for (const pkg of packages) {
        const path = join(root, pkg.project);
        if (!existsSync(path)) {
            findings.push(`${pkg.project}:1 [package-project] allowlisted project does not exist`);
            continue;
        }
        const text = readFileSync(path, 'utf8');
        const ids = [...text.matchAll(/<PackageId\b[^>]*>\s*([^<]+?)\s*<\/PackageId>/gi)].map(match => match[1]);
        const assemblies = [...text.matchAll(/<AssemblyName\b[^>]*>\s*([^<]+?)\s*<\/AssemblyName>/gi)].map(match => match[1]);
        if (!/<IsPackable\b[^>]*>\s*true\s*<\/IsPackable>/i.test(text) ||
            ![...ids, ...assemblies].includes(pkg.id) || ids.some(id => id !== pkg.id)) {
            findings.push(`${pkg.project}:1 [package-project] expected IsPackable true and package identity ${pkg.id}, with no foreign PackageId`);
        }
    }
}

function checkMavenProjects(root, mavenPackages, findings) {
    for (const pkg of mavenPackages) {
        const path = join(root, pkg.pom);
        if (!existsSync(path)) {
            findings.push(`${pkg.pom}:1 [maven-project] allowlisted Maven project does not exist`);
            continue;
        }
        const text = readFileSync(path, 'utf8');
        const own = ['parent', 'dependencyManagement', 'dependencies', 'build', 'profiles']
            .reduce((remaining, tag) => remaining.replace(new RegExp(`<${tag}\\b[^>]*>[\\s\\S]*?</${tag}>`, 'gi'), ''), text);
        const tag = name => own.match(new RegExp(`<${name}\\b[^>]*>\\s*([^<]*?)\\s*</${name}>`, 'i'))?.[1];
        const packaging = tag('packaging');
        if (tag('groupId') !== pkg.groupId || tag('artifactId') !== pkg.artifactId || (packaging !== undefined && packaging !== 'jar') || /<modules\b/i.test(own)) {
            findings.push(`${pkg.pom}:1 [maven-project] expected jar packaging, no modules and identity ${pkg.groupId}:${pkg.artifactId}`);
        }
    }
}

function checkPublishWorkflow(root, packages, publishWorkflow, findings, mavenPackages) {
    const path = join(root, publishWorkflow);
    if (!existsSync(path)) {
        findings.push(`${publishWorkflow}:1 [publish-workflow] publish workflow does not exist`);
        return;
    }
    const lines = readFileSync(path, 'utf8').split(/\r?\n/);
    const logical = logicalLines(lines.join('\n'));
    for (const pkg of packages) {
        if (!lines.some(line => hasPack(line, pkg.project))) {
            findings.push(`${publishWorkflow}:1 [publish-workflow] missing dotnet pack ${pkg.project}`);
        }
    }
    const scan = lines.findIndex(line => /package-allowlist\.mjs\b(?!\s+--packed)/.test(line));
    const deploys = logical.flatMap(({ index, text }) => commands(text).filter(command => isApprovedDeploy(command, mavenPackages)).map(() => index));
    const firstPush = lines.findIndex(line => rules[0][1].test(line));
    if (scan < 0 || deploys.length !== 1 || scan >= deploys[0] || (firstPush >= 0 && deploys[0] >= firstPush)) {
        findings.push(`${publishWorkflow}:1 [publish-workflow] scan with package-allowlist.mjs, then run exactly one Maven deploy of an allowlisted pom with -f, before the first nuget push`);
    }
    const verify = lines.findIndex(line => /package-allowlist\.mjs\s+--packed\b/.test(line));
    const push = lines.findIndex(line => rules[0][1].test(line));
    if (verify < 0 || push < 0 || verify >= push) {
        findings.push(`${publishWorkflow}:1 [publish-workflow] verify the packed set with package-allowlist.mjs --packed before the first nuget push`);
    }
}

export function scan({ root, allow = DEFAULT_ALLOW, packages = PACKAGES, mavenPackages = MAVEN_PACKAGES, publishWorkflow = PUBLISH_WORKFLOW }) {
    root = resolve(root);
    const result = { files: 0, workflows: 0, findings: [] };
    checkAllowlist(root, allow, result.findings);
    checkProjects(root, packages, result.findings);
    checkMavenProjects(root, mavenPackages, result.findings);
    checkPublishWorkflow(root, packages, publishWorkflow, result.findings, mavenPackages);
    inspectFiles(root, allow, result, packages, publishWorkflow, mavenPackages);
    if (result.workflows === 0) result.findings.push('.github/workflows:1 [workflows] no workflows to inspect');
    return result;
}

export function checkPacked(directory, packages = PACKAGES) {
    const entries = readdirSync(directory, { withFileTypes: true });
    const accepted = new Set();
    for (const pkg of packages) {
        const pattern = new RegExp(`^${escapeRegex(pkg.id)}\\.\\d+\\.\\d+\\.\\d+(?:-[0-9A-Za-z.-]+)?\\.nupkg$`);
        const matches = entries.filter(entry => entry.isFile() && pattern.test(entry.name));
        if (matches.length !== 1) throw new Error(`Expected exactly one packed file for ${pkg.id}`);
        accepted.add(matches[0].name);
    }
    if (entries.length !== accepted.size) throw new Error('Packed directory contains extra or foreign files');
    return [...accepted];
}

export function main(args = process.argv.slice(2)) {
    try {
        let root = process.cwd();
        let packed;
        for (let index = 0; index < args.length; index++) {
            const arg = args[index];
            if (!['--root', '--packed'].includes(arg) || !args[index + 1] || args[index + 1].startsWith('--')) throw new Error(`Unknown or incomplete argument: ${arg}`);
            const value = resolve(args[++index]);
            if (arg === '--root') root = value;
            else packed = value;
        }
        const result = scan({ root });
        const counts = `checked ${result.files} files (${result.workflows} workflows).`;
        if (result.findings.length > 0) {
            console.error(`Package allowlist check failed:\n${result.findings.join('\n')}\n${counts}`);
            return 1;
        }
        const names = packed ? checkPacked(packed) : [];
        console.log(`Package allowlist check passed: ${counts}${names.length ? ` Accepted: ${names.join(', ')}` : ''}`);
        return 0;
    } catch (error) {
        console.error(`Package allowlist check failed: ${error.message}`);
        return 1;
    }
}

if (process.argv[1] && existsSync(process.argv[1]) && realpathSync(process.argv[1]) === fileURLToPath(import.meta.url)) process.exitCode = main();
