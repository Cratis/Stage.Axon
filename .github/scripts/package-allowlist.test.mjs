// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import { scan, PACKAGES, PUBLISH_WORKFLOW } from './package-allowlist.mjs';

const script = fileURLToPath(new URL('./package-allowlist.mjs', import.meta.url));
const repository = resolve(dirname(script), '../..');
const workflow = '.github/workflows/build.yml';
const renderer = PACKAGES[0];
const projectText = `<IsPackable>true</IsPackable><AssemblyName>${renderer.id}</AssemblyName>`;
const publishText = `dotnet pack ${renderer.project}\nnode .github/scripts/package-allowlist.mjs --packed out\ndotnet nuget push out/*.nupkg`;

function fixture(t, files = {}) {
    const root = mkdtempSync(join(repository, '.ai-work/axon-publish/allowlist-test-'));
    t.after(() => rmSync(root, { recursive: true, force: true }));
    const defaults = {
        [workflow]: 'run: dotnet build',
        [renderer.project]: projectText,
        [PUBLISH_WORKFLOW]: publishText,
        '.github/scripts/package-allowlist.mjs': '',
        '.github/scripts/package-allowlist.test.mjs': ''
    };
    for (const [path, text] of Object.entries({ ...defaults, ...files })) {
        if (text === null) continue;
        mkdirSync(dirname(join(root, path)), { recursive: true });
        writeFileSync(join(root, path), text);
    }
    return root;
}

function findings(t, files, options = {}) {
    return scan({ root: fixture(t, files), ...options }).findings;
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
    ['registry-publish', 'run: gradle publish']
];

for (const [rule, text, line = 1] of violations) {
    test(`rejects ${rule} outside publish: ${text}`, t => {
        assert.ok(findings(t, { [workflow]: text }).some(f => f.startsWith(`${workflow}:${line} [${rule}]`)));
    });
    test(`publish workflow handles ${rule}: ${text}`, t => {
        const result = findings(t, { [PUBLISH_WORKFLOW]: `${publishText}\n${text}` });
        if (['nuget-push', 'package-file', 'registry-credentials'].includes(rule)) assert.deepEqual(result, []);
        else assert.ok(result.some(f => f.includes(`[${rule}]`)), result.join('\n'));
    });
}

for (const path of ['.github/actions/pack/action.yml', '.github/scripts/release.sh', 'Directory.Build.targets', 'Source/Renderer/Renderer.csproj', 'Directory.Build.props', 'build.proj', 'build.sln', 'build.slnx']) {
    test(`inspects ${path}`, t => {
        assert.ok(findings(t, { [path]: '<Exec Command="dotnet nuget push out/x"/>' }).some(f => f.startsWith(`${path}:1 [nuget-push]`)));
    });
}

for (const text of ['run: ./mvnw -B -fae verify', '# deployment', 'run: dotnet build', `dotnet pack ${renderer.project} -c Release`]) {
    test(`allows ${text}`, t => assert.deepEqual(findings(t, { [workflow]: text }), []));
}

for (const text of ['dotnet pack -c Release -o out', 'dotnet pack Stage.Axon.slnx']) {
    test(`rejects unscoped ${text}`, t => {
        assert.ok(findings(t, { [workflow]: text }).some(f => f.includes('[pack-scope]')));
    });
}

for (const path of ['Source/Other/Other.csproj', 'Source/Rendering.Conformance/Rendering.Conformance.csproj', 'Directory.Build.props']) {
    for (const value of ['true', '$(Foo)']) {
        test(`rejects packable ${path}: ${value}`, t => {
            assert.ok(findings(t, { [path]: `<IsPackable>\n${value}\n</IsPackable>` }).some(f => f.includes('[packable]')));
        });
    }
}

test('allows literal false case-insensitively', t => {
    assert.deepEqual(findings(t, { 'Other.csproj': '<IsPackable>FALSE</IsPackable>' }), []);
});

for (const path of [renderer.project, 'Other.csproj']) {
    test(`rejects automatic package generation in ${path}`, t => {
        assert.ok(findings(t, { [path]: `${projectText}<GeneratePackageOnBuild>true</GeneratePackageOnBuild>` }).some(f => f.includes('[packable]')));
    });
}

for (const text of [null, '<IsPackable>false</IsPackable>', '<IsPackable>true</IsPackable><AssemblyName>Foreign</AssemblyName>', `${projectText}<PackageId>Foreign</PackageId>`]) {
    test(`requires an existing packable project with the right identity: ${text}`, t => {
        assert.ok(findings(t, { [renderer.project]: text }).some(f => f.includes('[package-project]')));
    });
}

test('accepts PackageId as the package identity', t => {
    assert.deepEqual(findings(t, { [renderer.project]: projectText.replaceAll('AssemblyName', 'PackageId') }), []);
});

for (const text of [null, 'node .github/scripts/package-allowlist.mjs --packed out\nnuget push out/x', `dotnet pack ${renderer.project}\nnuget push out/x\nnode .github/scripts/package-allowlist.mjs --packed out`, `dotnet pack ${renderer.project}\nnuget push out/x`]) {
    test(`requires publish workflow, scoped pack and verification before push: ${text}`, t => {
        assert.ok(findings(t, { [PUBLISH_WORKFLOW]: text }).some(f => f.includes('[publish-workflow]')));
    });
}

test('supports injectable package and publish allowlists', t => {
    const root = fixture(t, { [renderer.project]: null, [PUBLISH_WORKFLOW]: null,
        'Custom.csproj': '<IsPackable>true</IsPackable><PackageId>Custom</PackageId>',
        '.github/workflows/custom.yml': 'dotnet pack Custom.csproj\nnode package-allowlist.mjs --packed out\nnuget push out/x' });
    assert.deepEqual(scan({ root, packages: [{ project: 'Custom.csproj', id: 'Custom' }], publishWorkflow: '.github/workflows/custom.yml' }).findings, []);
});

test('requires workflows even when other files are inspected', t => {
    const root = fixture(t, { [workflow]: null, [PUBLISH_WORKFLOW]: null, '.github/scripts/check.mjs': '// check' });
    const result = scan({ root });
    assert.equal(result.workflows, 0);
    assert.ok(result.findings.some(f => f.includes('no workflows to inspect')));
});

test('rejects a missing .github directory', t => {
    const root = fixture(t, { [workflow]: null, [PUBLISH_WORKFLOW]: null,
        '.github/scripts/package-allowlist.mjs': null, '.github/scripts/package-allowlist.test.mjs': null });
    assert.ok(scan({ root }).findings.some(f => f.includes('no workflows to inspect')));
});

test('counts both workflow extensions', t => {
    const result = scan({ root: fixture(t, { '.github/workflows/check.yaml': '' }) });
    assert.equal(result.workflows, 3);
    assert.deepEqual(result.findings, []);
});

for (const directory of ['bin', 'obj', 'node_modules', 'packages', 'artifacts', 'target', '.ai-work', '.git', '.hidden']) {
    test(`skips ${directory} at any depth`, t => {
        assert.deepEqual(findings(t, { [`${directory}/bad.props`]: 'dotnet nuget push x', [`.github/${directory}/bad.yml`]: 'dotnet nuget push x' }), []);
    });
}

test('skips an existing allowlisted file', t => {
    const path = '.github/scripts/allowed.mjs';
    assert.deepEqual(findings(t, { [path]: 'dotnet nuget push x' }, { allow: [path] }), []);
});

test('rejects an obsolete allowlist entry', t => {
    assert.ok(findings(t, {}, { allow: ['missing.mjs'] }).some(f => f.startsWith('missing.mjs:1 [allowlist]')));
});

function cli(root, args = []) {
    return spawnSync(process.execPath, [script, '--root', root, ...args], { encoding: 'utf8' });
}

test('CLI reports findings and counts on stderr', t => {
    const result = cli(fixture(t, { [workflow]: 'dotnet nuget push x' }));
    assert.equal(result.status, 1);
    assert.match(result.stderr, /Package allowlist check failed:/);
    assert.match(result.stderr, /build.yml:1 \[nuget-push\]/);
    assert.match(result.stderr, /checked \d+ files \(2 workflows\)/);
});

test('CLI reports passing counts on stdout', t => {
    const result = cli(fixture(t));
    assert.equal(result.status, 0, result.stderr);
    assert.match(result.stdout, /Package allowlist check passed: checked [1-9]\d* files \(2 workflows\)/);
    assert.equal(result.stderr, '');
});

for (const names of [[`${renderer.id}.1.2.3.nupkg`], [`${renderer.id}.1.2.3-preview.1.nupkg`], [], ['Foreign.1.2.3.nupkg'], [`${renderer.id}.Verification.1.2.3.nupkg`], [`${renderer.id}.1.2.3.nupkg`, 'extra.txt'], [`${renderer.id}.1.2.3.nupkg`, `${renderer.id}.1.2.3.snupkg`], [`${renderer.id}.1.2.3.nupkg`, `${renderer.id}.1.2.4.nupkg`]]) {
    test(`packed directory must contain exactly the allowlisted package: ${names}`, t => {
        const root = fixture(t);
        const packed = join(root, 'packed');
        mkdirSync(packed);
        for (const name of names) writeFileSync(join(packed, name), '');
        const result = cli(root, ['--packed', packed]);
        const valid = names.length === 1 && names[0].startsWith(`${renderer.id}.1.`);
        assert.equal(result.status, valid ? 0 : 1, result.stderr);
        if (valid) assert.ok(result.stdout.includes(names[0]));
    });
}

test('rejects a directory masquerading as a package', t => {
    const root = fixture(t);
    const packed = join(root, 'packed');
    mkdirSync(join(packed, `${renderer.id}.1.2.3.nupkg`), { recursive: true });
    assert.equal(cli(root, ['--packed', packed]).status, 1);
});

test('rejects unknown and incomplete arguments', t => {
    for (const args of [['--unknown'], ['--packed'], ['--root']]) assert.equal(cli(fixture(t), args).status, 1);
});

test('real repository CLI succeeds with a nonempty scan', () => {
    const result = cli(repository);
    assert.equal(result.status, 0, result.stderr);
    assert.match(result.stdout, /Package allowlist check passed: checked [1-9]\d* files \([1-9]\d* workflows\)/);
});
