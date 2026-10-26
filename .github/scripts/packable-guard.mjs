// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

import { execFileSync } from 'node:child_process';
import { readdirSync, readFileSync, realpathSync, existsSync } from 'node:fs';
import { dirname, resolve, join, basename } from 'node:path';
import { fileURLToPath } from 'node:url';

// Run after restore for each requested configuration. No build or restore is performed.
// Supported matrix: Debug/Release, declared TargetFramework(s), portable plus declared
// RuntimeIdentifier(s). Custom project-reference global-property remapping is rejected.
const configurations = ['Debug', 'Release'];
const excludedDirectories = new Set(['bin', 'obj', 'node_modules', 'packages', 'artifacts', 'output', 'cache', 'target', 'dist']);
const properties = ['IsPackable', 'PackageId', 'AssemblyName', 'TargetFramework', 'TargetFrameworks', 'TargetFrameworkMoniker', 'RuntimeIdentifier', 'RuntimeIdentifiers', 'ProjectAssetsFile'];
const forbidden = /^Cratis\.Screenplay\.Generation/i;

function requireValue(condition, message) {
    if (!condition) throw new Error(message);
}

function object(value, location) {
    requireValue(value && typeof value === 'object' && !Array.isArray(value), `Missing or malformed metadata: ${location}`);
    return value;
}

function text(value, location, allowEmpty = false) {
    requireValue(typeof value === 'string' && (allowEmpty || value.trim().length > 0), `Missing or malformed metadata: ${location}`);
    return value.trim();
}

function jsonFile(path) {
    try {
        return object(JSON.parse(readFileSync(path, 'utf8')), path);
    } catch (error) {
        throw new Error(`Cannot read resolved metadata ${path}: ${error.message}`);
    }
}

function split(value) {
    return [...new Set(value.split(';').map(item => item.trim()).filter(Boolean))];
}

function canonical(path) {
    try {
        return realpathSync(path);
    } catch (error) {
        throw new Error(`Missing project ${path}: ${error.message}`);
    }
}

function sourceDirectory(entry) {
    if (!entry.isDirectory()) return false;
    if (entry.name.startsWith('.')) return false;
    return !excludedDirectories.has(entry.name.toLowerCase());
}

function discover(directory) {
    const projects = [];
    for (const entry of readdirSync(directory, { withFileTypes: true })) {
        if (sourceDirectory(entry)) {
            projects.push(...discover(join(directory, entry.name)));
        } else if (entry.isFile() && entry.name.endsWith('.csproj')) {
            projects.push(canonical(join(directory, entry.name)));
        }
    }
    return projects.sort();
}

export function evaluateMSBuild(project, context) {
    const args = ['msbuild', project, '-nologo', '-verbosity:quiet', `-property:Configuration=${context.configuration}`,
        `-getProperty:${properties.join(',')}`, '-getItem:ProjectReference,PackageReference,PackageVersion'];
    if (context.framework) args.push(`-property:TargetFramework=${context.framework}`, `-property:RuntimeIdentifier=${context.runtime}`);
    else if (context.runtime) args.push(`-property:RuntimeIdentifier=${context.runtime}`);
    try {
        return JSON.parse(execFileSync('dotnet', args, { encoding: 'utf8', timeout: 60000, maxBuffer: 16 * 1024 * 1024 }));
    } catch (error) {
        throw new Error(`MSBuild evaluation failed: ${project} [${context.configuration}/${context.framework || 'outer'}/${context.runtime || 'portable'}]: ${error.stderr || error.message}`);
    }
}

function evaluated(evaluate, project, context) {
    const data = object(evaluate(project, context), project);
    const props = object(data.Properties, `${project}: Properties`);
    for (const name of properties) text(props[name], `${project}: ${name}`, true);
    requireValue(/^(true|false)$/i.test(props.IsPackable), `Invalid IsPackable in ${project}: ${props.IsPackable}`);
    text(props.PackageId || props.AssemblyName, `${project}: PackageId/AssemblyName`);
    const items = object(data.Items, `${project}: Items`);
    for (const name of ['ProjectReference', 'PackageReference', 'PackageVersion']) {
        requireValue(Array.isArray(items[name]), `Missing or malformed metadata: ${project}: ${name}`);
        for (const item of items[name]) text(object(item, `${project}: ${name}`).Identity, `${project}: ${name} Identity`);
    }
    return { props, items };
}

function frameworks(props, project) {
    const result = split(props.TargetFrameworks || props.TargetFramework);
    requireValue(result.length > 0, `No supported target frameworks in ${project}`);
    return result;
}

function label(state) {
    return `${state.project} [${state.configuration}/${state.framework}/${state.runtime || 'portable'}]`;
}

function checkIdentity(identity, path) {
    if (forbidden.test(identity)) throw new Error(`Forbidden dependency: ${[...path, identity].join(' -> ')}`);
}

function projectReferences(state) {
    return state.items.ProjectReference.map(item => {
        for (const name of ['AdditionalProperties', 'GlobalPropertiesToRemove', 'SetConfiguration', 'SetPlatform', 'SetTargetFramework', 'UndefineProperties']) {
            requireValue(!item[name], `Unsupported ProjectReference ${name} in ${label(state)} -> ${item.Identity}`);
        }
        return canonical(resolve(dirname(state.project), item.Identity));
    });
}

function lowerKeys(value) {
    return new Map(Object.entries(value).map(([key, entry]) => [key.toLowerCase(), entry]));
}

function nugetVersion(value, location) {
    const version = text(value, location).toLowerCase();
    const match = /^(\d+(?:\.\d+){0,3})(?:-([0-9a-z-]+(?:\.[0-9a-z-]+)*))?(?:\+([0-9a-z-]+(?:\.[0-9a-z-]+)*))?$/.exec(version);
    requireValue(match, `Malformed/unsupported NuGet version ${value} in ${location}`);
    const core = match[1].split('.').map(Number);
    requireValue(core.every(part => part <= 2147483647), `Unsupported NuGet version component ${value} in ${location}`);
    while (core.length < 4) core.push(0);
    const prerelease = match[2]?.split('.') ?? [];
    requireValue(!prerelease.some(part => /^0\d+$/.test(part)), `Unsupported leading-zero prerelease ${value} in ${location}`);
    // NuGet ignores build metadata for version precedence and identity.
    return { core, prerelease };
}

function compareIdentifier(left, right) {
    if (left === right) return 0;
    const leftNumeric = /^\d+$/.test(left);
    const rightNumeric = /^\d+$/.test(right);
    if (leftNumeric && rightNumeric) return BigInt(left) < BigInt(right) ? -1 : 1;
    if (leftNumeric !== rightNumeric) return leftNumeric ? -1 : 1;
    return left < right ? -1 : 1;
}

function comparePrerelease(left, right) {
    if (left.length === 0 && right.length === 0) return 0;
    if (left.length === 0) return 1;
    if (right.length === 0) return -1;
    for (let index = 0; index < Math.min(left.length, right.length); index++) {
        const compared = compareIdentifier(left[index], right[index]);
        if (compared !== 0) return compared;
    }
    return Math.sign(left.length - right.length);
}

function compareVersions(left, right) {
    for (let index = 0; index < 4; index++) {
        const compared = Math.sign(left.core[index] - right.core[index]);
        if (compared !== 0) return compared;
    }
    return comparePrerelease(left.prerelease, right.prerelease);
}

function checkIntervalOrder(range, value, location) {
    if (!range.minimum || !range.maximum) return;
    const compared = compareVersions(range.minimum, range.maximum);
    if (compared < 0) return;
    requireValue(compared === 0 && range.includeMinimum && range.includeMaximum, `Empty/reversed NuGet range ${value} in ${location}`);
}

function intervalRange(match, value, location) {
    const minimum = match[2].trim() ? nugetVersion(match[2], location) : null;
    const maximum = match[3].trim() ? nugetVersion(match[3], location) : null;
    const includeMinimum = match[1] === '[';
    const includeMaximum = match[4] === ']';
    requireValue(minimum || !includeMinimum, `Inclusive unbounded NuGet range ${value} in ${location}`);
    requireValue(maximum || !includeMaximum, `Inclusive unbounded NuGet range ${value} in ${location}`);
    const range = { minimum, maximum, includeMinimum, includeMaximum };
    checkIntervalOrder(range, value, location);
    return range;
}

function versionRange(value, location) {
    const range = text(value, location);
    const interval = /^(\[|\()([^,]*),([^,]*)(\]|\))$/.exec(range);
    if (interval) return intervalRange(interval, value, location);
    const exact = /^\[([^,]+)\]$/.exec(range);
    const minimum = nugetVersion(exact ? exact[1] : range, location);
    return { minimum, maximum: exact ? minimum : null, includeMinimum: true, includeMaximum: Boolean(exact) };
}

function sameBound(left, right) {
    if (!left || !right) return left === right;
    return compareVersions(left, right) === 0;
}

function sameRange(left, right) {
    return left.includeMinimum === right.includeMinimum && left.includeMaximum === right.includeMaximum
        && sameBound(left.minimum, right.minimum) && sameBound(left.maximum, right.maximum);
}

function satisfiesBound(version, bound, inclusive, direction) {
    if (!bound) return true;
    const compared = compareVersions(version, bound);
    return compared * direction > 0 || (compared === 0 && inclusive);
}

function checkResolvedVersion(node, requested, location) {
    const range = versionRange(requested, location);
    const lower = satisfiesBound(node.version, range.minimum, range.includeMinimum, 1);
    const upper = satisfiesBound(node.version, range.maximum, range.includeMaximum, -1);
    requireValue(lower && upper, `Resolved version ${node.id}/${node.versionText} does not satisfy ${requested}: ${location}`);
}

function checkPackageVersions(state, dependencies) {
    const central = lowerKeys(Object.fromEntries(state.items.PackageVersion.map(item => [item.Identity, item])));
    for (const item of state.items.PackageReference) {
        const version = item.VersionOverride || item.Version || central.get(item.Identity.toLowerCase())?.Version;
        const location = `${label(state)}: PackageReference ${item.Identity}`;
        requireValue(sameRange(versionRange(version, location), versionRange(dependencies.get(item.Identity.toLowerCase()).version, state.assetsPath)),
            `Stale package version: ${location} (${state.assetsPath})`);
    }
}

function checkDirectPackages(state, assetsFramework, nodes, path) {
    const declared = new Set(state.items.PackageReference.map(item => item.Identity.toLowerCase()));
    const dependencies = lowerKeys(object(assetsFramework.dependencies ?? {}, `${state.assetsPath}: framework dependencies`));
    for (const name of declared) {
        requireValue(dependencies.has(name), `Unresolved/stale PackageReference: ${path.join(' -> ')} -> ${name} (${state.assetsPath})`);
    }
    for (const [name, metadata] of dependencies) {
        object(metadata, `${state.assetsPath}: dependency ${name}`);
        requireValue(declared.has(name) || metadata.autoReferenced === true, `Stale/unevaluated PackageReference ${name} in ${state.assetsPath} for ${label(state)}`);
        const node = nodes.get(name);
        requireValue(node?.type === 'package', `Unresolved package: ${path.join(' -> ')} -> ${name} (${state.assetsPath})`);
        checkResolvedVersion(node, metadata.version, `${path.join(' -> ')} -> ${name} (${state.assetsPath})`);
    }
    checkPackageVersions(state, dependencies);
    return [...dependencies.keys()];
}

function targetNodes(assets, state) {
    const targetName = `${state.framework}${state.runtime ? `/${state.runtime}` : ''}`;
    const target = object(object(assets.targets, `${state.assetsPath}: targets`)[targetName], `${state.assetsPath}: targets/${targetName}`);
    const libraries = object(assets.libraries, `${state.assetsPath}: libraries`);
    const nodes = new Map();
    for (const [key, value] of Object.entries(target)) {
        const separator = key.lastIndexOf('/');
        requireValue(separator > 0 && separator < key.length - 1, `Malformed resolved library ${key} in ${state.assetsPath}`);
        const id = key.slice(0, separator);
        const node = object(value, `${state.assetsPath}: ${key}`);
        const library = object(libraries[key], `${state.assetsPath}: libraries/${key}`);
        requireValue(['package', 'project'].includes(node.type) && library.type === node.type, `Unresolved library ${key} in ${state.assetsPath}`);
        requireValue(!nodes.has(id.toLowerCase()), `Ambiguous resolved library ${id} in ${state.assetsPath}`);
        const versionText = key.slice(separator + 1);
        const version = nugetVersion(versionText, `${state.assetsPath}: resolved library ${key}`);
        nodes.set(id.toLowerCase(), { ...node, id, library, version, versionText });
    }
    return nodes;
}

function readAssets(state) {
    text(state.props.ProjectAssetsFile, `${label(state)}: ProjectAssetsFile`);
    state.assetsPath = resolve(dirname(state.project), state.props.ProjectAssetsFile);
    const assets = jsonFile(state.assetsPath);
    requireValue([3, 4].includes(assets.version), `Unsupported assets version in ${state.assetsPath}`);
    requireValue(!assets.logs || (Array.isArray(assets.logs) && assets.logs.every(log => typeof log?.level === 'string' && log.level.toLowerCase() !== 'error')),
        `Unresolved restore errors in ${state.assetsPath}`);
    const project = object(assets.project, `${state.assetsPath}: project`);
    const restore = object(project.restore, `${state.assetsPath}: project.restore`);
    requireValue(canonical(text(restore.projectPath, `${state.assetsPath}: projectPath`)) === state.project, `Mismatched projectPath in ${state.assetsPath}`);
    const framework = object(object(project.frameworks, `${state.assetsPath}: project.frameworks`)[state.framework], `${state.assetsPath}: framework ${state.framework}`);
    const restoreFramework = object(object(restore.frameworks, `${state.assetsPath}: restore.frameworks`)[state.framework], `${state.assetsPath}: restore framework ${state.framework}`);
    const restoredReferences = object(restoreFramework.projectReferences, `${state.assetsPath}: projectReferences`);
    const restoredPaths = Object.entries(restoredReferences).map(([key, value]) => canonical(resolve(dirname(state.project), text(object(value, `${state.assetsPath}: ${key}`).projectPath, `${state.assetsPath}: ${key} projectPath`))));
    const expected = projectReferences(state);
    requireValue(expected.length === restoredPaths.length && expected.every(path => restoredPaths.includes(path)), `Stale ProjectReference metadata in ${state.assetsPath} for ${label(state)}`);
    return { assets, framework, references: expected };
}

function dependencyEdges(nodes, state, path) {
    for (const node of nodes.values()) {
        const dependencies = object(node.dependencies ?? {}, `${state.assetsPath}: ${node.id} dependencies`);
        for (const [id, version] of Object.entries(dependencies)) {
            const location = `${[...path, node.id, id].join(' -> ')} (${state.assetsPath})`;
            const dependency = nodes.get(id.toLowerCase());
            requireValue(dependency, `Unresolved dependency: ${location}`);
            checkResolvedVersion(dependency, version, location);
        }
    }
}

function visitPackages(nodes, starts, path) {
    const visited = new Set();
    function visit(id, chain) {
        if (visited.has(id)) return;
        visited.add(id);
        const node = nodes.get(id);
        checkIdentity(node.id, chain);
        for (const dependency of Object.keys(node.dependencies ?? {})) visit(dependency.toLowerCase(), [...chain, node.id]);
    }
    for (const id of starts) visit(id, path);
    requireValue(visited.size === nodes.size, `Unreachable/stale resolved libraries in ${path.join(' -> ')}`);
}

function resolvedProjectNodes(nodes, state) {
    const result = new Map();
    for (const node of nodes.values()) {
        if (node.type !== 'project') continue;
        const path = text(node.library.msbuildProject || node.library.path, `${state.assetsPath}: ${node.id} project path`);
        const project = canonical(resolve(dirname(state.project), path));
        requireValue(!result.has(project), `Ambiguous resolved project ${project} in ${state.assetsPath}`);
        result.set(project, node);
    }
    return result;
}

function createGraph(evaluate) {
    const cache = new Map();
    function state(project, context) {
        const key = JSON.stringify([project, context.configuration, context.framework, context.runtime]);
        if (!cache.has(key)) {
            const data = evaluated(evaluate, project, context);
            requireValue(data.props.TargetFramework === context.framework && data.props.RuntimeIdentifier === context.runtime,
                `Mismatched evaluated framework/runtime in ${project}: ${JSON.stringify(context)}`);
            cache.set(key, { project, ...context, ...data });
        }
        return cache.get(key);
    }
    function referenceState(project, node, parent) {
        requireValue(node, `Unresolved ProjectReference: ${label(parent)} -> ${project}`);
        const selected = text(node.framework, `${parent.assetsPath}: referenced framework for ${project}`);
        const context = { configuration: parent.configuration, framework: '', runtime: parent.runtime };
        const { props } = evaluated(evaluate, project, context);
        const candidates = frameworks(props, project).map(framework => state(project, { ...context, framework }));
        const matches = candidates.filter(candidate => selected === candidate.framework || selected === candidate.props.TargetFrameworkMoniker);
        requireValue(matches.length === 1, `Unsupported referenced framework ${selected}: ${label(parent)} -> ${project}`);
        return matches[0];
    }
    function visit(current, chain = [], visiting = new Set()) {
        const path = [...chain, label(current)];
        requireValue(!visiting.has(label(current)), `Cyclic ProjectReference: ${path.join(' -> ')}`);
        const next = new Set(visiting).add(label(current));
        checkIdentity(current.props.PackageId, path);
        checkIdentity(current.props.AssemblyName, path);
        checkIdentity(basename(current.project, '.csproj'), path);
        const { assets, framework, references } = readAssets(current);
        const nodes = targetNodes(assets, current);
        const starts = checkDirectPackages(current, framework, nodes, path);
        dependencyEdges(nodes, current, path);
        const projects = resolvedProjectNodes(nodes, current);
        for (const project of references) {
            const node = projects.get(project);
            const referenced = referenceState(project, node, current);
            checkIdentity(node.id, path);
            starts.push(node.id.toLowerCase());
            visit(referenced, path, next);
        }
        visitPackages(nodes, starts, path);
    }
    return { state, visit };
}

function packableStates(project, configuration, graph, evaluate) {
    const { props } = evaluated(evaluate, project, { configuration, framework: '', runtime: '' });
    return frameworks(props, project).flatMap(framework => {
        const portable = graph.state(project, { configuration, framework, runtime: '' });
        const runtimes = new Set(['', ...split(props.RuntimeIdentifiers), ...split(props.RuntimeIdentifier), ...split(portable.props.RuntimeIdentifiers)]);
        return [...runtimes].map(runtime => graph.state(project, { configuration, framework, runtime }))
            .filter(state => state.props.IsPackable.toLowerCase() === 'true');
    });
}

export function guard({ root = process.cwd(), requestedConfigurations = configurations, evaluate = evaluateMSBuild } = {}) {
    requireValue(requestedConfigurations.length > 0 && requestedConfigurations.every(value => configurations.includes(value)), 'Supported configurations: Debug, Release');
    const projects = discover(resolve(root));
    const graph = createGraph(evaluate);
    const roots = [];
    for (const configuration of new Set(requestedConfigurations)) {
        for (const project of projects) {
            roots.push(...packableStates(project, configuration, graph, evaluate));
        }
    }
    requireValue(roots.length > 0, `Zero packable roots in ${resolve(root)} [${requestedConfigurations.join(', ')}]; dependency guard cannot pass vacuously.`);
    for (const rootState of roots) graph.visit(rootState);
    return `Packable dependency guard passed: ${roots.length} root matrix entries (${projects.length} discovered projects).`;
}

function argumentsFor(args) {
    const options = { requestedConfigurations: [] };
    for (let index = 0; index < args.length; index += 2) {
        const value = args[index + 1];
        requireValue(value && !value.startsWith('--'), `Missing value for ${args[index]}`);
        if (args[index] === '--root') options.root = value;
        else if (args[index] === '--configuration') options.requestedConfigurations.push(value);
        else throw new Error(`Unknown argument: ${args[index]}`);
    }
    if (options.requestedConfigurations.length === 0) options.requestedConfigurations = configurations;
    return options;
}

// Injection is an API for synthetic unit fixtures only; the CLI has no fixture bypass.
export function main(args = process.argv.slice(2), dependencies = {}) {
    try {
        console.log(guard({ ...argumentsFor(args), ...dependencies }));
        return 0;
    } catch (error) {
        console.error(`Packable dependency guard failed: ${error.message}`);
        return 1;
    }
}

if (process.argv[1] && existsSync(process.argv[1]) && canonical(process.argv[1]) === fileURLToPath(import.meta.url)) process.exitCode = main();
