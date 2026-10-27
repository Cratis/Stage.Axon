// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

import { existsSync, readFileSync, realpathSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const CLI_PROPS = 'https://raw.githubusercontent.com/Cratis/cli/main/Directory.Packages.props';

export function readStageVersion(text) {
    const versions = [...text.matchAll(/<StageVersion\b[^>]*>\s*([^<\s]+)\s*<\/StageVersion>/g)];
    if (versions.length !== 1) throw new Error('Expected exactly one StageVersion element');
    return versions[0][1];
}

export function compare(localText, cliText) {
    const localVersion = readStageVersion(localText);
    const cliVersion = readStageVersion(cliText);
    const contracts = [...localText.matchAll(/<PackageVersion\b[^>]*\bInclude="Cratis\.Stage\.Contracts"[^>]*>/g)];
    if (contracts.length !== 1 || !/\bVersion="\$\(StageVersion\)"/.test(contracts[0][0])) {
        throw new Error('Cratis.Stage.Contracts must use Version="$(StageVersion)"');
    }
    return { ok: localVersion === cliVersion, localVersion, cliVersion };
}

async function readProps(source) {
    if (!/^https?:\/\//.test(source)) return readFileSync(source, 'utf8');
    const response = await fetch(source, { signal: AbortSignal.timeout(30000) });
    if (response.status !== 200) throw new Error(`Fetching ${source} returned HTTP ${response.status}`);
    return response.text();
}

export async function main(args = process.argv.slice(2)) {
    try {
        let root = process.cwd();
        let source = CLI_PROPS;
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
        if (!result.ok) {
            console.error(`Stage.Axon StageVersion ${result.localVersion} differs from Cratis/cli main ${result.cliVersion}; bump StageVersion in Directory.Packages.props`);
            return 1;
        }
        console.log(`Stage.Axon StageVersion ${result.localVersion} matches Cratis/cli main`);
        return 0;
    } catch (error) {
        console.error(`CLI StageVersion check failed: ${error.message}`);
        return 1;
    }
}

if (process.argv[1] && existsSync(process.argv[1]) && realpathSync(process.argv[1]) === fileURLToPath(import.meta.url)) process.exitCode = await main();
