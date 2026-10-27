// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import { compare, readStageVersion } from './cli-stage-version.mjs';

const script = fileURLToPath(new URL('./cli-stage-version.mjs', import.meta.url));
const stage = version => `<StageVersion Condition="'$(StageVersion)' == ''">${version}</StageVersion>`;
const local = version => `${stage(version)}\n<PackageVersion Include="Cratis.Stage.Contracts" Version="$(StageVersion)" />`;

function cli(t, localText = local('4.203.0'), cliText = stage('4.203.0'), args = []) {
    const root = mkdtempSync(join(tmpdir(), 'axon-version-test-'));
    t.after(() => rmSync(root, { recursive: true, force: true }));
    mkdirSync(root, { recursive: true });
    writeFileSync(join(root, 'Directory.Packages.props'), localText);
    const props = join(root, 'cli.props');
    writeFileSync(props, cliText);
    return spawnSync(process.execPath, [script, '--root', root, '--cli-props', props, ...args], { encoding: 'utf8' });
}

test('compares equal versions and accepts the CLI Condition attribute', t => {
    assert.equal(compare(local('4.203.0'), stage('4.203.0')).ok, true);
    const result = cli(t);
    assert.equal(result.status, 0, result.stderr);
    assert.equal(result.stdout.trim(), 'Stage.Axon StageVersion 4.203.0 matches Cratis/cli main');
});

test('reports both different versions and the bump instruction', t => {
    assert.equal(compare(local('4.197.0'), stage('4.203.0')).ok, false);
    const result = cli(t, local('4.197.0'));
    assert.equal(result.status, 1);
    assert.match(result.stderr, /4\.197\.0.*4\.203\.0/);
    assert.match(result.stderr, /bump StageVersion in Directory.Packages.props/);
});

for (const text of ['', `${stage('4.203.0')}${stage('4.203.0')}`]) {
    for (const side of ['local', 'cli']) {
        test(`rejects ${side} with missing or duplicate StageVersion: ${text}`, t => {
            const result = side === 'local' ? cli(t, text) : cli(t, local('4.203.0'), text);
            assert.equal(result.status, 1);
            assert.match(result.stderr, /exactly one StageVersion/);
        });
    }
}

test('requires Contracts to use StageVersion', t => {
    const result = cli(t, local('4.203.0').replace('Version="$(StageVersion)"', 'Version="4.203.0"'));
    assert.equal(result.status, 1);
    assert.match(result.stderr, /Cratis.Stage.Contracts.*Version="\$\(StageVersion\)"/);
});

test('parses a semver from the real repository', () => {
    const text = readFileSync(new URL('../../Directory.Packages.props', import.meta.url), 'utf8');
    assert.match(readStageVersion(text), /^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$/);
});

test('rejects unknown and incomplete arguments', t => {
    for (const args of [['--unknown'], ['--cli-props'], ['--root']]) {
        const result = cli(t, undefined, undefined, args);
        assert.equal(result.status, 1);
        assert.match(result.stderr, /Unknown or incomplete argument/);
    }
});
