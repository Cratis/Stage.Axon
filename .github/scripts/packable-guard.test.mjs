// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, writeFileSync, readFileSync, rmSync } from 'node:fs';
import { dirname, resolve, join, relative } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { spawnSync } from 'node:child_process';
import { guard } from './packable-guard.mjs';

// These fixtures model MSBuild's JSON and NuGet's resolved assets, NOT MSBuild
// integration. They require neither an SDK nor network packages. The parent must
// separately exercise real metadata evaluation before enabling graph enforcement.
const repository = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const forbiddenPackage = 'cRaTiS.sCrEeNpLaY.gEnErAtIoN.Synthetic';
const packageReference = (id, metadata = {}) => ({ Identity: id, Version: '1.0.0', ...metadata });
const packageNode = (dependencies = {}) => ({ type: 'package', dependencies });

function writeJson(path, value) {
    mkdirSync(dirname(path), { recursive: true });
    writeFileSync(path, JSON.stringify(value, null, 2));
}

function moniker(framework) {
    return `.NETCoreApp,Version=v${framework.slice(3)}`;
}

function selected(definition, context) {
    return { ...definition, ...definition.configurations?.[context.configuration], ...definition.frameworks?.[context.framework], ...definition.runtimes?.[context.runtime] };
}

function fixtureRange(version) {
    if (version.startsWith('[') || version.startsWith('(')) return version;
    return `[${version}, )`;
}

function resolvedAssets(project, definitions, name, context) {
    const definition = selected(definitions[name], context);
    const target = { ...definition.packages };
    const projectReferences = {};
    const libraries = {};
    for (const reference of definition.references ?? []) {
        const referencePath = join(dirname(dirname(project)), reference, `${reference}.csproj`);
        const child = selected(definitions[reference], context);
        const framework = child.targetFramework ?? context.framework;
        const id = child.packageId ?? reference;
        target[`${id}/1.0.0`] = { type: 'project', framework: moniker(framework), dependencies: {} };
        libraries[`${id}/1.0.0`] = { type: 'project', msbuildProject: relative(dirname(project), referencePath) };
        projectReferences[referencePath] = { projectPath: referencePath };
    }
    for (const [key, node] of Object.entries(target)) libraries[key] ??= { type: node.type };
    const dependencies = Object.fromEntries((definition.direct ?? []).map(item => [item.Identity, {
        target: 'Package', version: fixtureRange(item.Version || definition.central?.[item.Identity])
    }]));
    return {
        version: 3,
        targets: { [`${context.framework}${context.runtime ? `/${context.runtime}` : ''}`]: target },
        libraries,
        project: {
            restore: { projectPath: project, frameworks: { [context.framework]: { projectReferences } } },
            frameworks: { [context.framework]: { dependencies } }
        }
    };
}

function evaluation(project, definition, context, assetsPath) {
    return {
        Properties: {
            IsPackable: String(definition.packable ?? false),
            PackageId: definition.packageId ?? definition.name,
            AssemblyName: definition.assemblyName ?? definition.name,
            TargetFramework: context.framework || definition.targetFramework || 'net10.0',
            TargetFrameworks: definition.targetFrameworks ?? '',
            TargetFrameworkMoniker: moniker(context.framework || 'net10.0'),
            RuntimeIdentifier: context.runtime,
            RuntimeIdentifiers: definition.runtimeIdentifiers ?? '',
            ProjectAssetsFile: assetsPath
        },
        Items: {
            ProjectReference: (definition.references ?? []).map(name => ({ Identity: `../${name}/${name}.csproj`, ...definition.referenceMetadata })),
            PackageReference: definition.direct ?? [],
            PackageVersion: Object.entries(definition.central ?? {}).map(([Identity, Version]) => ({ Identity, Version }))
        }
    };
}

function fixtureContexts(definition) {
    const frameworks = ['', ...(definition.targetFrameworks || definition.targetFramework || 'net10.0').split(';')];
    const runtimes = ['', ...(definition.runtimeIdentifiers?.split(';') ?? [])];
    return ['Debug', 'Release'].flatMap(configuration => frameworks.flatMap(framework => runtimes.map(runtime => ({ configuration, framework, runtime }))));
}

function addProject(root, definitions, name, metadata) {
    const definition = definitions[name];
    const project = join(root, name, `${name}.csproj`);
    mkdirSync(dirname(project), { recursive: true });
    writeFileSync(project, '<!-- Synthetic fixture: intentionally not an MSBuild evaluator. -->');
    for (const context of fixtureContexts(definition)) {
        const { configuration, framework, runtime } = context;
        const value = { ...selected(definition, context), name };
        const assetsPath = join(root, '.resolved', name, configuration, framework || 'outer', runtime || 'portable', 'project.assets.json');
        metadata[JSON.stringify([project, configuration, framework, runtime])] = evaluation(project, value, context, assetsPath);
        if (framework) writeJson(assetsPath, resolvedAssets(project, definitions, name, context));
    }
    return project;
}

function fixture(t, definitions) {
    const base = join(repository, '.ai-work/axon4');
    mkdirSync(base, { recursive: true });
    const root = mkdtempSync(join(base, 'guard-fixture-'));
    // Only this test's newly created private fixture directory is disposable.
    t.after(() => rmSync(root, { recursive: true, force: true }));
    const projects = {};
    const metadata = {};
    for (const name of Object.keys(definitions)) projects[name] = addProject(root, definitions, name, metadata);
    const evaluate = (project, context) => {
        const key = JSON.stringify([project, context.configuration, context.framework, context.runtime]);
        assert.ok(metadata[key], `Unexpected fixture evaluation: ${key}`);
        return structuredClone(metadata[key]);
    };
    const options = { root, evaluate, requestedConfigurations: ['Release'] };
    const data = (name = 'Renderer', configuration = 'Release', framework = 'net10.0', runtime = '') => metadata[JSON.stringify([projects[name], configuration, framework, runtime])];
    const alterAssets = (change, ...state) => {
        const path = data(...state).Properties.ProjectAssetsFile;
        const assets = JSON.parse(readFileSync(path, 'utf8'));
        change(assets);
        writeJson(path, assets);
    };
    return { root, projects, metadata, options, data, alterAssets };
}

function clean(packable = true) {
    return { packable, direct: [packageReference('Synthetic.Clean')], packages: { 'Synthetic.Clean/1.0.0': packageNode() } };
}

function forbidden(packable = true) {
    return { packable, direct: [packageReference(forbiddenPackage, { PrivateAssets: 'all' })], packages: { [`${forbiddenPackage}/1.0.0`]: packageNode() } };
}

test('clean packable resolved graph passes without inspecting source XML', t => {
    const f = fixture(t, { Renderer: clean() });
    assert.match(guard(f.options), /passed: 1 root matrix entries/);
});

test('reverse nonpackable RoundTrip -> Renderer edge does not contaminate Renderer', t => {
    const f = fixture(t, { Renderer: clean(), RoundTrip: { ...forbidden(false), references: ['Renderer'] } });
    assert.match(guard(f.options), /passed/);
});

test('direct forbidden PrivateAssets=all package fails case-insensitively', t => {
    const f = fixture(t, { Renderer: forbidden() });
    assert.throws(() => guard(f.options), /Forbidden dependency: .*Renderer.*cRaTiS\.sCrEeNpLaY\.gEnErAtIoN\.Synthetic/);
});

test('outgoing project traversal through nonpackable Bridge reports transitive package path', t => {
    const f = fixture(t, {
        Renderer: { packable: true, references: ['Bridge'] },
        Bridge: {
            packable: false, direct: [packageReference('Synthetic.Adapter', { PrivateAssets: 'all' })],
            packages: { 'Synthetic.Adapter/1.0.0': packageNode({ [forbiddenPackage]: '1.0.0' }), [`${forbiddenPackage}/1.0.0`]: packageNode() }
        }
    });
    assert.throws(() => guard(f.options), /Forbidden dependency: .*Renderer.* -> .*Bridge.* -> Synthetic\.Adapter -> cRaTiS/);
});

test('parent resolved project edges are checked even when resolution differs from child assets', t => {
    const f = fixture(t, { Renderer: { packable: true, references: ['Bridge'] }, Bridge: clean(false) });
    f.alterAssets(assets => {
        assets.targets['net10.0']['Bridge/1.0.0'].dependencies = { 'Synthetic.Adapter': '1.0.0' };
        assets.targets['net10.0']['Synthetic.Adapter/1.0.0'] = packageNode({ [forbiddenPackage]: '1.0.0' });
        assets.targets['net10.0'][`${forbiddenPackage}/1.0.0`] = packageNode();
        assets.libraries['Synthetic.Adapter/1.0.0'] = { type: 'package' };
        assets.libraries[`${forbiddenPackage}/1.0.0`] = { type: 'package' };
    });
    assert.throws(() => guard(f.options), /Forbidden dependency: .*Renderer.* -> Bridge -> Synthetic\.Adapter -> cRaTiS/);
});

test('unused central version is not a dependency; a resolved central reference is', t => {
    const definition = { packable: true, central: { [forbiddenPackage]: '1.0.0' } };
    const unused = fixture(t, { Renderer: definition });
    assert.match(guard(unused.options), /passed/);
    const referenced = fixture(t, { Renderer: { ...definition, direct: [{ Identity: forbiddenPackage }], packages: { [`${forbiddenPackage}/1.0.0`]: packageNode() } } });
    assert.throws(() => guard(referenced.options), /Forbidden dependency/);
});

test('evaluated Debug-only package fails Debug and passes Release', t => {
    const f = fixture(t, { Renderer: { ...clean(), configurations: { Debug: forbidden() } } });
    assert.match(guard(f.options), /passed/);
    assert.throws(() => guard({ ...f.options, requestedConfigurations: ['Debug'] }), /Forbidden dependency: .*Debug\/net10.0/);
    assert.throws(() => guard({ root: f.root, evaluate: f.options.evaluate }), /Forbidden dependency/);
});

test('every declared framework is checked, including framework-conditional dependencies', t => {
    const f = fixture(t, { Renderer: { ...clean(), targetFrameworks: 'net9.0;net10.0', frameworks: { 'net9.0': forbidden() } } });
    assert.throws(() => guard(f.options), /Forbidden dependency: .*Release\/net9.0/);
});

test('a multi-target reference follows the resolved compatible framework, not unrelated child targets', t => {
    const f = fixture(t, {
        Renderer: { packable: true, references: ['Bridge'] },
        Bridge: { ...clean(false), targetFrameworks: 'net9.0;net10.0', frameworks: { 'net9.0': forbidden(false) } }
    });
    assert.match(guard(f.options), /passed/);
});

test('every declared RID is checked', t => {
    const f = fixture(t, { Renderer: { ...clean(), runtimeIdentifiers: 'linux-x64', runtimes: { 'linux-x64': forbidden() } } });
    assert.throws(() => guard(f.options), /Forbidden dependency: .*linux-x64/);
});

for (const identity of ['packageId', 'assemblyName']) {
    test(`forbidden referenced project ${identity} fails even when nonpackable`, t => {
        const f = fixture(t, { Renderer: { packable: true, references: ['Bridge'] }, Bridge: { packable: false, [identity]: forbiddenPackage } });
        assert.throws(() => guard(f.options), /Forbidden dependency: .*Renderer.*cRaTiS/);
    });
}

test('zero packable roots fails before requiring restored assets', t => {
    const f = fixture(t, { Renderer: clean(false) });
    rmSync(join(f.root, '.resolved'), { recursive: true });
    assert.throws(() => guard(f.options), /Zero packable roots.*cannot pass vacuously/);
});

test('no discovered projects also fails zero-root', t => {
    const f = fixture(t, {});
    assert.throws(() => guard(f.options), /Zero packable roots/);
});

test('output and cache projects are excluded from discovery', t => {
    const f = fixture(t, { Renderer: clean() });
    for (const directory of ['bin', 'obj', '.git', 'node_modules', 'cache', 'output']) {
        mkdirSync(join(f.root, directory), { recursive: true });
        writeFileSync(join(f.root, directory, 'Ignored.csproj'), 'not a project');
    }
    assert.match(guard(f.options), /1 discovered projects/);
});

test('missing and malformed assets fail with their diagnostic path', t => {
    const f = fixture(t, { Renderer: clean() });
    const path = f.data().Properties.ProjectAssetsFile;
    rmSync(path);
    assert.throws(() => guard(f.options), /Cannot read resolved metadata .*project.assets.json/);
    writeFileSync(path, '{invalid');
    assert.throws(() => guard(f.options), /Cannot read resolved metadata .*project.assets.json/);
});

const invalidAssets = {
    'missing target': assets => { assets.targets = {}; },
    'missing library': assets => { assets.libraries = {}; },
    'missing project frameworks': assets => { delete assets.project.frameworks; },
    'missing restore references': assets => { delete assets.project.restore.frameworks['net10.0'].projectReferences; },
    'unresolved direct package': assets => { assets.targets['net10.0'] = {}; },
    'unresolved transitive edge': assets => { assets.targets['net10.0']['Synthetic.Clean/1.0.0'].dependencies = { 'Synthetic.Missing': '1.0.0' }; },
    'restore errors': assets => { assets.logs = [{ level: 'Error', code: 'NU1101' }]; },
    'wrong resolved library type': assets => { assets.libraries['Synthetic.Clean/1.0.0'].type = 'project'; },
    'stale conditional reference': assets => { assets.project.frameworks['net10.0'].dependencies = {}; },
    'unexpected resolved reference': assets => { assets.project.frameworks['net10.0'].dependencies['Synthetic.Stale'] = { version: '[1.0.0, )' }; },
    'stale package version': assets => { assets.project.frameworks['net10.0'].dependencies['Synthetic.Clean'].version = '[2.0.0, )'; },
    'unresolved dependency version': assets => { assets.targets['net10.0']['Synthetic.Clean/1.0.0'].dependencies = { 'Synthetic.Clean': null }; }
};
for (const [name, change] of Object.entries(invalidAssets)) {
    test(`${name} fails closed`, t => {
        const f = fixture(t, { Renderer: clean() });
        f.alterAssets(change);
        assert.throws(() => guard(f.options), /project.assets.json/);
    });
}

function directVersionFixture(t, requested, resolved) {
    return fixture(t, { Renderer: {
        packable: true,
        direct: [packageReference('Synthetic.Clean', { Version: requested })],
        packages: { [`Synthetic.Clean/${resolved}`]: packageNode() }
    } });
}

function transitiveVersionFixture(t, requested, resolved) {
    return fixture(t, { Renderer: {
        packable: true, direct: [packageReference('Synthetic.Clean')],
        packages: {
            'Synthetic.Clean/1.0.0': packageNode({ 'Synthetic.Child': requested }),
            [`Synthetic.Child/${resolved}`]: packageNode()
        }
    } });
}

test('direct exact request rejects a target-key version outside its range', t => {
    const f = directVersionFixture(t, '[2.0.0]', '1.0.0');
    assert.throws(() => guard(f.options), /Resolved version Synthetic\.Clean\/1.0.0 does not satisfy \[2.0.0\].*project.assets.json/);
});

test('transitive request rejects a target-key version outside its range', t => {
    const f = transitiveVersionFixture(t, '[2.0.0]', '1.0.0');
    assert.throws(() => guard(f.options), /Resolved version Synthetic\.Child\/1.0.0 does not satisfy \[2.0.0\].*Synthetic.Clean -> Synthetic.Child.*project.assets.json/);
});

const satisfyingVersions = [
    ['[1.0.0]', '1.0.0'],
    ['1.0.0', '1.5.0'],
    ['[1.0.0,2.0.0)', '1.9.9'],
    ['(1.0.0,2.0.0]', '2.0.0'],
    ['(,1.0.0]', '1.0.0'],
    ['[1.0.0,)', '2.0.0'],
    ['(,)', '2.0.0'],
    ['[1.2]', '1.2.0.0'],
    ['[1.0.0.1,1.0.0.3)', '1.0.0.2'],
    ['[1.0.0+requested]', '1.0.0+resolved'],
    ['[1.0.0-beta.2,1.0.0)', '1.0.0-beta.10'],
    ['[1.0.0-2,1.0.0-alpha]', '1.0.0-10'],
    ['[1.0.0-beta,1.0.0-beta.1)', '1.0.0-beta'],
    ['[1.0.0-BETA.2]', '1.0.0-beta.2'],
    ['[1.0.0-beta.2,1.0.0]', '1.0.0']
];
for (const [requested, resolved] of satisfyingVersions) {
    test(`direct and transitive range ${requested} accepts ${resolved}`, t => {
        const direct = directVersionFixture(t, requested, resolved);
        const transitive = transitiveVersionFixture(t, requested, resolved);
        assert.match(guard(direct.options), /passed/);
        assert.match(guard(transitive.options), /passed/);
    });
}

const unsatisfyingVersions = [
    ['1.0.0', '0.9.9'],
    ['[1.0.0,2.0.0)', '2.0.0'],
    ['(1.0.0,2.0.0]', '1.0.0'],
    ['(,1.0.0)', '1.0.0'],
    ['(1.0.0,)', '1.0.0'],
    ['[1.0.0]', '1.0.0-beta'],
    ['[1.0.0-beta.10,1.0.0)', '1.0.0-beta.2'],
    ['[1.0.0-beta.2,1.0.0-beta.10)', '1.0.0-beta.10'],
    ['[1.0.0-beta.1,1.0.0)', '1.0.0-beta'],
    ['[1.0.0-alpha]', '1.0.0-10'],
    ['[1.0.0.2,1.0.0.3)', '1.0.0.1']
];
for (const [requested, resolved] of unsatisfyingVersions) {
    test(`direct and transitive range ${requested} rejects ${resolved}`, t => {
        const direct = directVersionFixture(t, requested, resolved);
        const transitive = transitiveVersionFixture(t, requested, resolved);
        assert.throws(() => guard(direct.options), /Resolved version .* does not satisfy .*project.assets.json/);
        assert.throws(() => guard(transitive.options), /Resolved version .* does not satisfy .*project.assets.json/);
    });
}

const malformedRanges = ['garbage', '[garbage,)', '[1.0.0,garbage)', '[2.0.0,1.0.0]', '[1.0.0,1.0.0)',
    '[,1.0.0]', '[1.0.0,]', '[1.0.0,2.0.0,3.0.0)', '1.*', '[1.0.*,2.0.0)', '1.0.0-beta..1', '[1.0.0', '1. 0.0'];
for (const requested of malformedRanges) {
    test(`malformed or unsupported direct/transitive range ${requested} fails even with a matching ID`, t => {
        const direct = directVersionFixture(t, requested, '1.0.0');
        const transitive = transitiveVersionFixture(t, requested, '1.0.0');
        assert.throws(() => guard(direct.options), /NuGet (version|range).*project.assets.json/);
        assert.throws(() => guard(transitive.options), /NuGet (version|range).*project.assets.json/);
    });
}

for (const resolved of ['garbage', '1.*', '[1.0.0]', '1.0.0-beta..1', '1.0.0.0.0', '1.0.0-beta.01', '2147483648.0.0']) {
    test(`malformed or unsupported resolved target-key version ${resolved} fails closed`, t => {
        const f = directVersionFixture(t, '1.0.0', resolved);
        assert.throws(() => guard(f.options), /(?:NuGet version|leading-zero prerelease).*project.assets.json.*resolved library/);
    });
}

test('equivalent normalized evaluated and restored version ranges retain freshness', t => {
    const f = directVersionFixture(t, '[1.2.0]', '1.2.0');
    f.data().Items.PackageReference[0].Version = '[1.2]';
    assert.match(guard(f.options), /passed/);
});

test('stale project references fail rather than dropping the outgoing edge', t => {
    const f = fixture(t, { Renderer: { packable: true, references: ['Bridge'] }, Bridge: clean(false) });
    f.alterAssets(assets => { assets.project.restore.frameworks['net10.0'].projectReferences = {}; });
    assert.throws(() => guard(f.options), /Stale ProjectReference metadata/);
});

test('a restored reference missing its target node fails', t => {
    const f = fixture(t, { Renderer: { packable: true, references: ['Bridge'] }, Bridge: clean(false) });
    f.alterAssets(assets => { assets.targets['net10.0'] = {}; });
    assert.throws(() => guard(f.options), /Unresolved ProjectReference/);
});

test('unsupported reference framework and property remapping fail explicitly', t => {
    const f = fixture(t, { Renderer: { packable: true, references: ['Bridge'] }, Bridge: clean(false) });
    f.alterAssets(assets => { assets.targets['net10.0']['Bridge/1.0.0'].framework = 'unknown-framework'; });
    assert.throws(() => guard(f.options), /Unsupported referenced framework/);
    f.data().Items.ProjectReference[0].AdditionalProperties = 'Configuration=Debug';
    assert.throws(() => guard(f.options), /Unsupported ProjectReference AdditionalProperties/);
});

test('cyclic project references fail with a dependency path', t => {
    const f = fixture(t, { Renderer: { packable: true, references: ['Bridge'] }, Bridge: { references: ['Renderer'] } });
    assert.throws(() => guard(f.options), /Cyclic ProjectReference: .*Renderer.*Bridge.*Renderer/);
});

test('malformed evaluation, evaluation failures and unsupported matrices fail closed', t => {
    const f = fixture(t, { Renderer: clean() });
    assert.throws(() => guard({ ...f.options, evaluate: () => { throw new Error('Synthetic evaluation failure'); } }), /Synthetic evaluation failure/);
    assert.throws(() => guard({ ...f.options, evaluate: () => ({ Properties: {} }) }), /Missing or malformed metadata/);
    assert.throws(() => guard({ ...f.options, requestedConfigurations: ['Custom'] }), /Supported configurations/);
    delete f.data().Items.PackageReference;
    assert.throws(() => guard(f.options), /PackageReference/);
});

test('mismatched evaluated TFM/RID fails instead of assuming the requested context', t => {
    const f = fixture(t, { Renderer: clean() });
    f.data().Properties.TargetFramework = 'net9.0';
    assert.throws(() => guard(f.options), /Mismatched evaluated framework\/runtime/);
});

test('the real CLI exits nonzero for an empty repository without requiring an SDK', t => {
    const f = fixture(t, {});
    const result = spawnSync(process.execPath, [fileURLToPath(new URL('./packable-guard.mjs', import.meta.url)), '--root', f.root], { encoding: 'utf8' });
    assert.equal(result.status, 1, result.stderr);
    assert.match(result.stderr, /Zero packable roots/);
    assert.doesNotMatch(result.stdout, /passed/);
});

test('negative forbidden fixture drives the CLI exit contract, not only helper assertions', t => {
    const f = fixture(t, { Renderer: forbidden() });
    const metadataPath = join(f.root, 'evaluated.json');
    writeJson(metadataPath, f.metadata);
    const runner = join(f.root, 'fixture-cli.mjs');
    writeFileSync(runner, `import { readFileSync } from 'node:fs';
import { main } from ${JSON.stringify(pathToFileURL(join(repository, '.github/scripts/packable-guard.mjs')).href)};
const metadata = JSON.parse(readFileSync(${JSON.stringify(metadataPath)}, 'utf8'));
process.exitCode = main(process.argv.slice(2), { evaluate: (project, context) => metadata[JSON.stringify([project, context.configuration, context.framework, context.runtime])] });
`);
    const result = spawnSync(process.execPath, [runner, '--root', f.root, '--configuration', 'Release'], { encoding: 'utf8' });
    assert.equal(result.status, 1, result.stderr);
    assert.match(result.stderr, /Forbidden dependency: .*Renderer.*cRaTiS/);
    assert.doesNotMatch(result.stdout, /passed/);
});
