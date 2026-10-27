// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

import { existsSync, readFileSync, realpathSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const CLI_PROPS = 'https://raw.githubusercontent.com/Cratis/cli/main/Directory.Packages.props';
const PAIRS = [
    { packageName: 'Cratis.Stage.Contracts', property: 'StageVersion' },
    { packageName: 'Cratis.Screenplay', property: 'ScreenplayVersion' }
];

function parseVersion(version) {
    const match = /^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$/.exec(version);
    if (!match) throw new Error(`Unparseable semver: ${version}`);
    const prerelease = match[4] ? match[4].split('.') : [];
    if (prerelease.some(part => /^0\d+$/.test(part))) throw new Error(`Unparseable semver: ${version}`);
    return { core: match.slice(1, 4).map(BigInt), prerelease };
}

export function readVersion(text, property) {
    const openings = [...text.matchAll(new RegExp(`<${property}\\b[^>]*>`, 'g'))];
    const match = new RegExp(`<${property}\\b[^>]*>([\\s\\S]*?)<\\/${property}>`).exec(text);
    if (openings.length !== 1 || !match) throw new Error(`Expected exactly one ${property} element`);
    const version = match[1].trim();
    try {
        parseVersion(version);
    } catch (error) {
        throw new Error(`${property}: ${error.message}`);
    }
    return version;
}

function order(left, right) {
    if (left === right) return 0;
    return left < right ? -1 : 1;
}

function compareIdentifier(left, right) {
    const leftNumeric = /^\d+$/.test(left);
    const rightNumeric = /^\d+$/.test(right);
    if (leftNumeric && rightNumeric) return order(BigInt(left), BigInt(right));
    if (leftNumeric !== rightNumeric) return leftNumeric ? -1 : 1;
    return order(left, right);
}

function compareParts(left, right, comparator) {
    for (let index = 0; index < Math.max(left.length, right.length); index++) {
        if (left[index] === undefined) return -1;
        if (right[index] === undefined) return 1;
        const result = comparator(left[index], right[index]);
        if (result !== 0) return result;
    }
    return 0;
}

export function compareVersions(left, right) {
    const local = parseVersion(left);
    const cli = parseVersion(right);
    const coreOrder = compareParts(local.core, cli.core, order);
    if (coreOrder !== 0) return coreOrder;
    if (local.prerelease.length === 0 && cli.prerelease.length > 0) return 1;
    if (cli.prerelease.length === 0 && local.prerelease.length > 0) return -1;
    return compareParts(local.prerelease, cli.prerelease, compareIdentifier);
}

function checkPackageProperty(text, pair) {
    const packageName = pair.packageName.replaceAll('.', '\\.');
    const packages = [...text.matchAll(new RegExp(`<PackageVersion\\b[^>]*\\bInclude="${packageName}"[^>]*>`, 'g'))];
    const versionAttribute = new RegExp(`\\bVersion="\\$\\(${pair.property}\\)"`);
    if (packages.length !== 1 || !versionAttribute.test(packages[0][0])) {
        throw new Error(`${pair.packageName} must use Version="$(${pair.property})"`);
    }
}

function comparePair(localText, cliText, pair) {
    const localVersion = readVersion(localText, pair.property);
    const cliVersion = readVersion(cliText, pair.property);
    checkPackageProperty(localText, pair);
    const comparison = compareVersions(localVersion, cliVersion);
    const sameMajor = parseVersion(localVersion).core[0] === parseVersion(cliVersion).core[0];
    let status = 'equal';
    if (!sameMajor) status = 'major-mismatch';
    else if (comparison > 0) status = 'ahead';
    else if (comparison < 0) status = 'behind';
    return { ...pair, localVersion, cliVersion, status };
}

export function withoutComments(text) {
    return text.replace(/<!--[\s\S]*?-->/g, '');
}

export function compare(localText, cliText) {
    const [local, cli] = [withoutComments(localText), withoutComments(cliText)];
    const pairs = PAIRS.map(pair => comparePair(local, cli, pair));
    return { ok: pairs.every(pair => ['equal', 'behind'].includes(pair.status)), pairs };
}

async function readProps(source) {
    try {
        if (!/^https?:\/\//.test(source)) return readFileSync(source, 'utf8');
        const response = await fetch(source, { signal: AbortSignal.timeout(30000) });
        if (response.status !== 200) throw new Error(`HTTP ${response.status}`);
        return await response.text();
    } catch (error) {
        throw new Error(`Unable to read CLI packages props from ${source}: ${error.message}`);
    }
}

function reportPair(pair) {
    const prefix = `${pair.packageName}: Stage.Axon ${pair.property} ${pair.localVersion}`;
    const target = `Cratis/cli main ${pair.cliVersion}`;
    if (pair.status === 'equal') console.log(`${prefix} matches ${target}`);
    else if (pair.status === 'behind') {
        console.log(`::warning::${prefix} is behind ${target}`);
        console.log(`One-line bump: bump ${pair.property} in Directory.Packages.props to ${pair.cliVersion}`);
    } else {
        const reason = pair.status === 'ahead' ? 'ahead of' : 'incompatible with';
        console.error(`${prefix} is ${reason} ${target}${pair.status === 'major-mismatch' ? ' (major versions differ)' : ''}`);
    }
}

export async function main(args = process.argv.slice(2)) {
    try {
        let root = process.cwd();
        let source = process.env.CLI_PACKAGES_PROPS || CLI_PROPS;
        for (let index = 0; index < args.length; index++) {
            const arg = args[index];
            if (!['--root', '--cli-props'].includes(arg) || !args[index + 1] || args[index + 1].startsWith('--')) {
                throw new Error(`Unknown or incomplete argument: ${arg}`);
            }
            const value = args[++index];
            if (arg === '--root') root = resolve(value);
            else source = value;
        }
        const result = compare(readFileSync(join(root, 'Directory.Packages.props'), 'utf8'), await readProps(source));
        result.pairs.forEach(reportPair);
        return result.ok ? 0 : 1;
    } catch (error) {
        console.error(`CLI version guard failed: ${error.message}`);
        return 1;
    }
}

if (process.argv[1] && existsSync(process.argv[1]) && realpathSync(process.argv[1]) === fileURLToPath(import.meta.url)) process.exitCode = await main();
