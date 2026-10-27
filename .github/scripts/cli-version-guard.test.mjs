// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import { compare, compareVersions, readVersion, main } from './cli-version-guard.mjs';

const script = fileURLToPath(new URL('./cli-version-guard.mjs', import.meta.url));
const property = (name, version) => `<${name} Condition="'\$(${name})' == ''">${version}</${name}>`;
const props = (stage = '4.203.0', screenplay = '4.250.0') => `${property('StageVersion', stage)}\n${property('ScreenplayVersion', screenplay)}`;
const local = (stage, screenplay) => `${props(stage, screenplay)}\n<PackageVersion Include="Cratis.Stage.Contracts" Version="$(StageVersion)" />\n<PackageVersion Include="Cratis.Screenplay" Version="$(ScreenplayVersion)" />`;

function cli(t, localText = local(), cliText = props(), args = []) {
    const root = mkdtempSync(join(tmpdir(), 'axon-version-test-'));
    t.after(() => rmSync(root, { recursive: true, force: true }));
    writeFileSync(join(root, 'Directory.Packages.props'), localText);
    const path = join(root, 'cli.props');
    writeFileSync(path, cliText);
    return spawnSync(process.execPath, [script, '--root', root, ...args], {
        encoding: 'utf8', env: { ...process.env, CLI_PACKAGES_PROPS: path }
    });
}

test('equal pairs succeed, accept Condition attributes and print both versions', t => {
    assert.equal(compare(local(), props()).ok, true);
    const result = cli(t);
    assert.equal(result.status, 0, result.stderr);
    assert.match(result.stdout, /Cratis.Stage.Contracts.*StageVersion 4\.203\.0 matches Cratis\/cli main 4\.203\.0/);
    assert.match(result.stdout, /Cratis.Screenplay.*ScreenplayVersion 4\.250\.0 matches Cratis\/cli main 4\.250\.0/);
    assert.doesNotMatch(result.stdout, /::warning::/);
});

for (const [name, stage, screenplay, packageName] of [
    ['StageVersion', '4.197.0', '4.250.0', 'Cratis.Stage.Contracts'],
    ['ScreenplayVersion', '4.203.0', '4.249.0', 'Cratis.Screenplay']
]) {
    test(`lower ${name} warns without failing and names the one-line bump`, t => {
        const result = cli(t, local(stage, screenplay));
        assert.equal(result.status, 0, result.stderr);
        assert.ok(result.stdout.includes(`::warning::${packageName}`));
        assert.match(result.stdout, new RegExp(`One-line bump: bump ${name} in Directory.Packages.props`));
        assert.equal(compare(local(stage, screenplay), props()).ok, true);
    });
}

for (const [name, stage, screenplay] of [
    ['StageVersion', '4.204.0', '4.250.0'],
    ['ScreenplayVersion', '4.203.0', '4.251.0']
]) {
    test(`higher ${name} fails independently while the other pair matches`, t => {
        const result = cli(t, local(stage, screenplay));
        assert.equal(result.status, 1);
        assert.match(result.stderr, /ahead of Cratis\/cli main/);
        assert.ok(result.stderr.includes(name));
        assert.match(result.stdout, /matches Cratis\/cli main/);
        assert.equal(compare(local(stage, screenplay), props()).ok, false);
    });
}

for (const [stage, screenplay] of [['3.999.0', '4.250.0'], ['5.0.0', '4.250.0'], ['4.203.0', '3.999.0'], ['4.203.0', '5.0.0']]) {
    test(`major mismatch fails even when Axon is lower: ${stage}, ${screenplay}`, t => {
        const result = cli(t, local(stage, screenplay));
        assert.equal(result.status, 1);
        assert.match(result.stderr, /major versions differ/);
    });
}

for (const name of ['StageVersion', 'ScreenplayVersion']) {
    for (const side of ['local', 'cli']) {
        for (const change of ['missing', 'duplicate', 'self-closing duplicate', 'invalid']) {
            test(`rejects ${side} ${name}: ${change}`, t => {
                const original = side === 'local' ? local() : props();
                const element = new RegExp(`<${name}\\b[^>]*>[^<]*<\\/${name}>`);
                const replacements = {
                    missing: '', duplicate: `${original.match(element)[0]}${original.match(element)[0]}`,
                    'self-closing duplicate': `${original.match(element)[0]}<${name} />`, invalid: `<${name}>not-semver</${name}>`
                };
                const changed = original.replace(element, replacements[change]);
                const result = side === 'local' ? cli(t, changed) : cli(t, local(), changed);
                assert.equal(result.status, 1);
                assert.ok(result.stderr.includes(name), result.stderr);
            });
        }
    }
}

for (const [packageName, name] of [['Cratis.Stage.Contracts', 'StageVersion'], ['Cratis.Screenplay', 'ScreenplayVersion']]) {
    test(`requires ${packageName} to use its version property`, t => {
        const result = cli(t, local().replace(`Version="$(${name})"`, 'Version="4.203.0"'));
        assert.equal(result.status, 1);
        assert.ok(result.stderr.includes(`${packageName} must use Version="$(${name})"`));
    });
}

const prereleases = ['4.203.0-alpha', '4.203.0-alpha.1', '4.203.0-alpha.beta', '4.203.0-beta', '4.203.0-beta.2', '4.203.0-beta.11', '4.203.0-rc.1', '4.203.0'];
for (let index = 1; index < prereleases.length; index++) {
    const lower = prereleases[index - 1];
    const higher = prereleases[index];
    test(`semver orders ${lower} before ${higher}`, t => {
        assert.equal(compareVersions(lower, higher), -1);
        assert.equal(compareVersions(higher, lower), 1);
        assert.equal(cli(t, local(lower), props(higher)).status, 0);
        assert.equal(cli(t, local(higher), props(lower)).status, 1);
    });
}

test('a commented-out property does not count as a pin', () => {
    const onlyCommented = `<!-- ${property('StageVersion', '4.203.0')} -->\n${property('ScreenplayVersion', '4.250.0')}`;
    assert.throws(() => compare(local(), onlyCommented), /StageVersion/);
});

test('a commented-out historical pin next to the active one is not a duplicate', () => {
    const withHistory = `<!-- ${property('StageVersion', '4.197.0')} -->\n${props()}`;
    assert.equal(compare(local(), withHistory).ok, true);
    const localWithHistory = `<!-- <PackageVersion Include="Cratis.Screenplay" Version="4.241.0" /> -->\n${local()}`;
    assert.equal(compare(localWithHistory, props()).ok, true);
});

test('semver compares numeric components and ignores build metadata', () => {
    assert.equal(compareVersions('4.9.0', '4.10.0'), -1);
    assert.equal(compareVersions('4.203.0+build.1', '4.203.0+build.2'), 0);
    assert.equal(compareVersions('4.203.0-9007199254740992', '4.203.0-9007199254740993'), -1);
});

for (const version of ['', '4.203', '04.203.0', '4.203.0-01', '4.203.0-alpha..1', '4.203.0+', '4.203.0 junk']) {
    test(`rejects unparseable semver: ${version}`, t => {
        assert.equal(cli(t, local(version)).status, 1);
    });
}

test('reads both real repository properties as semver', () => {
    const text = readFileSync(new URL('../../Directory.Packages.props', import.meta.url), 'utf8');
    for (const name of ['StageVersion', 'ScreenplayVersion']) assert.match(readVersion(text, name), /^\d+\.\d+\.\d+/);
});

test('fetch failure is fail-closed with a clear source message', async t => {
    t.mock.method(globalThis, 'fetch', async () => { throw new Error('unreachable'); });
    const errors = [];
    t.mock.method(console, 'error', message => errors.push(message));
    assert.equal(await main(['--cli-props', 'https://example.invalid/props']), 1);
    assert.match(errors.join('\n'), /Unable to read CLI packages props from https:\/\/example.invalid\/props: unreachable/);
});

test('HTTP errors fail closed', async t => {
    t.mock.method(globalThis, 'fetch', async () => ({ status: 503 }));
    const errors = [];
    t.mock.method(console, 'error', message => errors.push(message));
    assert.equal(await main(['--cli-props', 'https://example.invalid/props']), 1);
    assert.match(errors.join('\n'), /HTTP 503/);
});

test('rejects an unreadable CLI_PACKAGES_PROPS override', t => {
    const result = cli(t, undefined, undefined, ['--cli-props', join(tmpdir(), 'axon-no-such-props', 'missing.props')]);
    assert.equal(result.status, 1);
    assert.match(result.stderr, /Unable to read CLI packages props/);
});

test('rejects unknown and incomplete arguments', t => {
    for (const args of [['--unknown'], ['--cli-props'], ['--root']]) {
        const result = cli(t, undefined, undefined, args);
        assert.equal(result.status, 1);
        assert.match(result.stderr, /Unknown or incomplete argument/);
    }
});
