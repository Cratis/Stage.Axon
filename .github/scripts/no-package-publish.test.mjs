// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { tmpdir } from 'node:os';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import { scan } from './no-package-publish.mjs';

const script = fileURLToPath(new URL('./no-package-publish.mjs', import.meta.url));
const repository = resolve(dirname(script), '../..');
const workflow = '.github/workflows/build.yml';

function fixture(t, files = { [workflow]: 'run: dotnet build' }) {
    const root = mkdtempSync(join(tmpdir(), 'no-package-publish-'));
    t.after(() => rmSync(root, { recursive: true, force: true }));
    for (const [path, text] of Object.entries(files)) {
        mkdirSync(dirname(join(root, path)), { recursive: true });
        writeFileSync(join(root, path), text);
    }
    return root;
}

const violations = [
    ['nuget-push', 'run: dotnet nuget push out/x --source https://api.nuget.org/v3/index.json'],
    ['nuget-push', 'run: nuget.exe push out/x'],
    ['nuget-push', 'run: DOTNET NuGet PUSH out/x'],
    ['paket-push', 'run: paket push out/x'],
    ['paket-push', 'run: paket.exe push out/x'],
    ['registry-credentials', 'env: NUGET_API_KEY: example'],
    ['registry-credentials', 'env: NUGET_AUTH_TOKEN: example'],
    ['registry-credentials', 'run: tool --api-key example'],
    ['registry-credentials', 'uses: nuget/login@v1'],
    ['release-asset', 'uses: softprops/action-gh-release@v2'],
    ['release-asset', 'uses: softprops/action-gh-release@v2\nwith:\n  files: artifacts/*'],
    ['release-asset', 'uses: actions/upload-release-asset@v1'],
    ['release-asset', 'run: gh release upload v1 x'],
    ['release-asset', 'run: gh release create v1'],
    ['package-file', "uses: actions/upload-artifact@v4\nwith:\n  path: '**/*.nupkg'", 3],
    ['package-file', "uses: actions/upload-artifact@v4\nwith:\n  path: '*.snupkg'", 3],
    ['registry-publish', 'run: npm publish'],
    ['registry-publish', 'run: yarn publish'],
    ['registry-publish', 'run: pnpm publish'],
    ['registry-publish', 'run: ./mvnw -B deploy'],
    ['registry-publish', 'run: mvn deploy'],
    ['registry-publish', 'run: mvnw.cmd deploy'],
    ['registry-publish', 'run: ./gradlew publish'],
    ['registry-publish', 'run: gradle publish'],
    ['nuget-push', '# dotnet nuget push out/x']
];

for (const [rule, text, line = 1] of violations) {
    test(`rejects ${rule}: ${text}`, t => {
        const root = fixture(t, { [workflow]: text });
        const result = scan({ root, allow: [] });
        assert.equal(result.files, 1);
        assert.equal(result.workflows, 1);
        assert.ok(result.findings.some(finding => finding.startsWith(`${workflow}:${line} [${rule}]`)), result.findings.join('\n'));
    });
}

for (const path of ['.github/actions/pack/action.yml', '.github/scripts/release.sh', 'Directory.Build.targets', 'Source/Renderer/Renderer.csproj', 'Directory.Build.props', 'build.proj', 'build.sln', 'build.slnx']) {
    test(`inspects ${path}`, t => {
        const root = fixture(t, { [workflow]: 'run: dotnet build', [path]: '<Exec Command="dotnet nuget push out/x"/>' });
        const result = scan({ root, allow: [] });
        assert.equal(result.files, 2);
        assert.ok(result.findings.some(finding => finding.startsWith(`${path}:1 [nuget-push]`)), result.findings.join('\n'));
    });
}

for (const text of ['run: ./mvnw -B -fae verify', '# deployment', 'run: dotnet pack -c Release -o out', 'run: dotnet build']) {
    test(`allows ${text}`, t => {
        const result = scan({ root: fixture(t, { [workflow]: text }), allow: [] });
        assert.deepEqual(result, { files: 1, workflows: 1, findings: [] });
    });
}

test('requires workflows even when other files are inspected', t => {
    const result = scan({ root: fixture(t, { '.github/scripts/check.mjs': '// check' }), allow: [] });
    assert.equal(result.files, 1);
    assert.equal(result.workflows, 0);
    assert.ok(result.findings.some(finding => finding.includes('no workflows to inspect')));
});

test('rejects a missing .github directory', t => {
    const result = scan({ root: fixture(t, {}), allow: [] });
    assert.equal(result.files, 0);
    assert.equal(result.workflows, 0);
    assert.ok(result.findings.some(finding => finding.includes('no workflows to inspect')));
});

test('counts both workflow extensions', t => {
    const result = scan({ root: fixture(t, { [workflow]: '', '.github/workflows/check.yaml': '' }), allow: [] });
    assert.deepEqual(result, { files: 2, workflows: 2, findings: [] });
});

for (const directory of ['bin', 'obj', 'node_modules', 'packages', 'artifacts', 'target', '.ai-work', '.git', '.hidden']) {
    test(`skips ${directory} at any depth`, t => {
        const root = fixture(t, {
            [workflow]: 'run: dotnet build',
            [`${directory}/bad.props`]: 'dotnet nuget push x',
            [`.github/${directory}/bad.yml`]: 'dotnet nuget push x'
        });
        assert.deepEqual(scan({ root, allow: [] }), { files: 1, workflows: 1, findings: [] });
    });
}

test('skips an existing allowlisted file', t => {
    const path = '.github/scripts/allowed.mjs';
    const root = fixture(t, { [workflow]: '', [path]: 'dotnet nuget push x' });
    assert.deepEqual(scan({ root, allow: [path] }), { files: 1, workflows: 1, findings: [] });
});

test('rejects an obsolete allowlist entry', t => {
    const path = '.github/scripts/missing.mjs';
    const result = scan({ root: fixture(t), allow: [path] });
    assert.ok(result.findings.some(finding => finding.startsWith(`${path}:1 [allowlist]`)));
});

function cli(root) {
    return spawnSync(process.execPath, [script, '--root', root], { encoding: 'utf8' });
}

function cliFixture(t, text) {
    return fixture(t, {
        [workflow]: text,
        '.github/scripts/no-package-publish.mjs': '',
        '.github/scripts/no-package-publish.test.mjs': ''
    });
}

test('CLI fails with findings on stderr and reports counts', t => {
    const result = cli(cliFixture(t, 'run: dotnet nuget push x'));
    assert.equal(result.status, 1, result.stderr);
    assert.match(result.stderr, /Package publication check failed:/);
    assert.match(result.stderr, /build.yml:1 \[nuget-push\]/);
    assert.match(result.stderr, /checked 1 files \(1 workflows\)/);
});

test('CLI succeeds with counts on stdout', t => {
    const result = cli(cliFixture(t, 'run: dotnet build'));
    assert.equal(result.status, 0, result.stderr);
    assert.equal(result.stdout.trim(), 'No package publication: checked 1 files (1 workflows).');
    assert.equal(result.stderr, '');
});

test('real repository CLI succeeds with a nonempty scan', () => {
    const result = cli(repository);
    assert.equal(result.status, 0, result.stderr);
    assert.match(result.stdout, /No package publication: checked [1-9]\d* files \([1-9]\d* workflows\)\./);
});
