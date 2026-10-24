#!/usr/bin/env python3
# Copyright (c) Cratis. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

"""Build the spec matrix, guard solution coverage, and reject empty test runs.

Refresh measured prefix weights from directories of successful runs:
    python3 .github/spec-shards.py refresh-timings .ai-work/318/trx
Each immediate child of the TRX root is one run. A sharded project is refreshed from
the runs that contain its TRX files, which must be one per shard (four Rendering,
three Conformance); a project without TRX files keeps its stored weights.
A Rendering prefix weighs the summed wall-clock span of its test classes: xUnit's
per-test duration omits the Establish/Because a class shares, so it cannot be used.
A Conformance prefix weighs its wall-attributed seconds: the class span plus the idle
time before the class starts, which is where its shared fixture runs.
`estimate` reports those sums per shard.
"""

import argparse
from collections import Counter
from datetime import datetime
import json
import math
import os
from pathlib import Path
import re
import subprocess
from typing import NamedTuple
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
RENDERING = "Source/Rendering.Cratis/Rendering.Cratis.csproj"
NAMESPACE = "Cratis.Stage.Rendering.Cratis"
PLANNER = NAMESPACE + ".for_CratisArtifactRenderPlanner"
# Each fixture runs in exactly one dedicated workflow job (dotnet-build.yml) and in no shard.
STANDALONE_APPLICATION_FIXTURE = "when_verifying_a_standalone_generated_application"
DEDICATED_FIXTURES = (
    "when_compiling_generated_name_matrix",
    "when_building_the_generated_screen_composition_application",
    STANDALONE_APPLICATION_FIXTURE,
)
EXCLUSIONS = "&".join("FullyQualifiedName!~" + fixture for fixture in DEDICATED_FIXTURES)
BLUEPRINT_TEMPLATES_FIXTURE = "when_building_the_blueprint_templates_application"
RENDERING_SHARDS = 4
TIMINGS = ROOT / ".github/spec-shard-timings.json"
TRX_NAMESPACE = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
CONFORMANCE = "Source/Conformance.Specs/Conformance.Specs.csproj"
THREE_WAY = "Cratis.Stage.Conformance.Specs.for_ThreeWayConformance"


class Sharding(NamedTuple):
    """A test project split across shards by test-name prefix."""
    name: str          # Shard name stem and timings section.
    project: str
    source: str        # Directory scanned for spec sources.
    shards: int
    split: tuple       # Namespaces split per test class; every other for_* namespace stays together.
    every_class: bool  # Split every top-level test class in a file, not only the first declaration.
    assembly: str      # Identifies the project's TRX files; namespaces overlap between projects.
    measure: str       # "class-span" or "wall-attributed".


RENDERING_SHARDING = Sharding("rendering", RENDERING, "Source/Rendering.Cratis", RENDERING_SHARDS, (PLANNER,),
                              False, "Cratis.Stage.Rendering.Cratis.dll", "class-span")
# Conformance runs the planner namespace too (its RootNamespace is Rendering.Cratis), against its own assembly.
# Its shared class fixtures run before the first test starts, so plain class spans undercount it.
CONFORMANCE_SHARDING = Sharding("conformance", CONFORMANCE, "Source/Conformance.Specs", 3, (PLANNER, THREE_WAY),
                                True, "Cratis.Stage.Conformance.Specs.dll", "wall-attributed")
SHARDED = (RENDERING_SHARDING, CONFORMANCE_SHARDING)
FACT = re.compile(r"\[(?:Fact|Theory)(?:\(|\])")
TOP_LEVEL_CLASS = re.compile(r"^public (?:(?:sealed|abstract|partial|static) )*class (\w+)", re.MULTILINE)
# One static shard: Host is the slowest project, so it runs first. Specifications is one namespace.
PROJECT_SHARDS = {
    "small-projects": [
        "Source/Host/Host.csproj",
        "Source/Specifications/Specifications.csproj",
        "Source/Contracts/Contracts.csproj",
        "Source/Stage/Stage.csproj",
        "Source/SpecRunner/SpecRunner.csproj",
        "Source/Rendering.Cratis.Scaffolding/Rendering.Cratis.Scaffolding.csproj",
        "Source/Rendering.Axon/Rendering.Axon.csproj",
        "Source/Rendering.Axon.Verification/Rendering.Axon.Verification.csproj",
    ],
}


def source_groups(sharding=RENDERING_SHARDING):
    """Count spec sources (or test classes) per prefix; split the large namespaces by test class."""
    groups = Counter()
    source_root = ROOT / sharding.source
    for source in sorted(source_root.rglob("*.cs")):
        if {"bin", "obj"}.intersection(source.relative_to(source_root).parts):
            continue
        text = source.read_text()
        if not FACT.search(text):
            continue
        namespace = re.search(r"^namespace ([\w.]+);", text, re.MULTILINE)
        declaration = re.search(r"public (?:sealed |abstract |partial )*class (\w+)", text)
        if namespace is None or declaration is None:
            continue  # Unrecognized/new source shapes still run in the complement shard.
        name = namespace[1]
        if name.startswith(sharding.split):
            for test_class in (test_classes(text) if sharding.every_class else [declaration[1]]):
                groups[name + "." + test_class + "."] += 1
        else:
            # Keep each smaller for_* namespace (including its nested contexts) together.
            parts = name.split(".")
            end = next((i + 1 for i, part in enumerate(parts) if part.startswith("for_")), len(parts))
            prefix = ".".join(parts[:end]) + "."
            groups[prefix] += 1
    return merge_nested_groups(groups)


def test_classes(text):
    """Top-level classes whose body holds a test; one file can declare several test classes."""
    declarations = list(TOP_LEVEL_CLASS.finditer(text))
    ends = [declaration.start() for declaration in declarations[1:]] + [len(text)]
    return [declaration[1] for declaration, end in zip(declarations, ends)
            if FACT.search(text, declaration.start(), end)]


def merge_nested_groups(groups):
    """One prefix owns nested namespaces/classes, so positive filters cannot overlap."""
    merged = Counter()
    for prefix in sorted(groups, key=lambda name: (len(name), name)):
        parent = next((name for name in merged if prefix.casefold().startswith(name.casefold())), prefix)
        merged[parent] += groups[prefix]
    return merged


def timings_section(data, sharding):
    # Rendering keeps the original top-level format; every other sharded project has its own section.
    return data if sharding.name == RENDERING_SHARDING.name else data.get(sharding.name, {})


def weights(sharding, groups):
    measured = timings_section(json.loads(TIMINGS.read_text()), sharding).get("prefix_seconds", {})
    if any(not isinstance(value, (int, float)) or not math.isfinite(value) or value < 0
           for value in measured.values()):
        raise SystemExit("Spec shard timings must contain finite, non-negative seconds.")
    # One second per source file/class for unmeasured/new prefixes; discovery still owns coverage.
    return {prefix: measured.get(prefix, count) for prefix, count in groups.items()}


def partitions(sharding):
    prefix_weights = weights(sharding, source_groups(sharding))
    parts = [[] for _ in range(sharding.shards)]
    loads = [0.0] * sharding.shards
    for prefix in sorted(prefix_weights, key=lambda name: (-prefix_weights[name], name)):
        index = min(range(sharding.shards), key=lambda i: (loads[i], i))
        parts[index].append(prefix)
        loads[index] += prefix_weights[prefix]
    if any(not part for part in parts):
        raise SystemExit(f"{sharding.name} spec discovery must populate all {sharding.shards} partitions.")
    return parts


def shard_names(sharding):
    return [f"{sharding.name}-{index}" for index in range(1, sharding.shards)] + [sharding.name + "-remainder"]


def sharded_entries(sharding, exclusions):
    parts = partitions(sharding)
    names = shard_names(sharding)
    entries = []
    for name, prefixes in zip(names, parts[:-1]):
        selected = "|".join("FullyQualifiedName~" + prefix for prefix in prefixes)
        entries.append({"name": name, "projects": [sharding.project],
                        "filter": exclusions + "&(" + selected + ")"})
    # Negate EVERY positive prefix, not a snapshot of the remaining tests. New/unrecognized
    # namespaces and classes necessarily land here, even if discovery never sees their source.
    remainder = "&".join("FullyQualifiedName!~" + prefix for part in parts[:-1] for prefix in part)
    entries.append({"name": names[-1], "projects": [sharding.project], "filter": exclusions + "&" + remainder})
    return entries


def matrix(blueprint_templates_lane=False):
    exclusions = EXCLUSIONS
    if blueprint_templates_lane:
        exclusions += "&FullyQualifiedName!~" + BLUEPRINT_TEMPLATES_FIXTURE
    shards = [{"name": name, "projects": projects, "filter": exclusions}
              for name, projects in PROJECT_SHARDS.items()]
    for sharding in SHARDED:
        shards.extend(sharded_entries(sharding, exclusions))
    return {"include": shards}


def guard_solution(shards):
    projects = [project.attrib["Path"].replace("\\", "/")
                for project in ET.parse(ROOT / "Stage.slnx").iter("Project")]
    test_projects = set()
    for project in projects:
        result = subprocess.run(
            ["dotnet", "msbuild", project, "-nologo", "-verbosity:quiet",
             "-property:Configuration=Debug", "-getProperty:IsTestProject"],
            cwd=ROOT, check=True, capture_output=True, text=True,
        )
        if result.stdout.strip().lower() == "true":
            test_projects.add(project)
    assigned = Counter(project for shard in shards["include"] for project in shard["projects"])
    missing = test_projects - assigned.keys()
    extra = assigned.keys() - test_projects
    expected = {sharding.project: sharding.shards for sharding in SHARDED}
    duplicates = {project: count for project, count in assigned.items()
                  if count != expected.get(project, 1)}
    if missing or extra or duplicates or not test_projects:
        raise SystemExit(f"Invalid spec coverage: missing={sorted(missing)}, extra={sorted(extra)}, counts={duplicates}")
    print(f"All {len(test_projects)} solution test projects covered; "
          + ", ".join(f"{sharding.name} has {sharding.shards} shards" for sharding in SHARDED)
          + ", each with a disjoint complement partition.")


def require_executed(path):
    namespace = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
    root = ET.parse(path).getroot()
    counter = root.find("t:ResultSummary/t:Counters", namespace)
    results = root.findall("t:Results/t:UnitTestResult", namespace)
    executed = int(counter.get("executed", "0")) if counter is not None else 0
    if executed <= 0 or not any(result.get("outcome") == "Passed" for result in results):
        raise SystemExit(f"{path}: the filtered spec run must execute tests, not discover nothing or only skip.")
    print(f"{path}: {executed} tests executed")


def run_shard(shard):
    # Run every project even after a failure, so one failing project does not hide failures in the others.
    failures = []
    for project in shard["projects"]:
        name = Path(project).stem
        results = ROOT / "TestResults" / shard["name"] / name
        results.mkdir(parents=True, exist_ok=True)
        trx = results / "specs.trx"
        trx.unlink(missing_ok=True)  # A stale result must not satisfy an empty invocation.
        completed = subprocess.run(
            ["dotnet", "test", project, "--no-build", "--configuration", "Debug",
             "--filter", shard["filter"], "--logger", "trx;LogFileName=specs.trx",
             "--results-directory", str(results)], cwd=ROOT,
        )
        if completed.returncode != 0:
            failures.append(f"{name}: dotnet test exited {completed.returncode}")
            continue
        try:
            require_executed(trx)
        except SystemExit as error:
            failures.append(str(error))
    if failures:
        raise SystemExit("Spec shard failed:\n" + "\n".join(failures))


def class_bounds(root):
    """First test start and last test end of every class with a passed test."""
    classes = {test.get("id"): test.find("t:TestMethod", TRX_NAMESPACE).get("className")
               for test in root.findall("t:TestDefinitions/t:UnitTest", TRX_NAMESPACE)}
    bounds = {}
    for result in root.findall("t:Results/t:UnitTestResult", TRX_NAMESPACE):
        if result.get("outcome") != "Passed":
            continue
        start = datetime.fromisoformat(result.attrib["startTime"])
        end = datetime.fromisoformat(result.attrib["endTime"])
        if end < start:
            raise SystemExit("TRX test timings must be non-negative.")
        name = classes[result.attrib["testId"]]
        first, last = bounds.get(name, (start, end))
        bounds[name] = (min(first, start), max(last, end))
    return bounds


def class_spans(root):
    """Wall-clock seconds from a class's first test start to its last test end.

    A shared-lifecycle class runs Establish/Because once, and xUnit leaves that time out of every
    per-test duration. The span keeps it. Concurrent classes overlap, so spans sum to work, not wall time.
    """
    return {name: (last - first).total_seconds() for name, (first, last) in class_bounds(root).items()}


def wall_attributed(root):
    """Class span plus the idle time before the class starts, when no class in this TRX was running.

    A class fixture runs before the class's first test starts, so its cost shows up as a gap no
    span covers. Charging each gap to the class that ends it makes one TRX sum to about its test wall time.
    """
    seconds = {}
    running_until = None
    for first, last, name in sorted((first, last, name) for name, (first, last) in class_bounds(root).items()):
        idle = 0.0
        if running_until is not None and first > running_until:
            idle = (first - running_until).total_seconds()
        seconds[name] = (last - first).total_seconds() + idle
        running_until = last if running_until is None else max(running_until, last)
    return seconds


MEASURES = {"class-span": class_spans, "wall-attributed": wall_attributed}
MEASUREMENTS = {
    "class-span": "Mean per-run sum of test-class wall-clock spans (first startTime to last endTime) in "
                  "seconds; includes shared class setup. Dedicated-lane fixtures are excluded. Concurrent "
                  "classes overlap, so this is test work, not job wall time. Unmeasured prefixes use one "
                  "second per source file.",
    "wall-attributed": "Mean per-run sum of wall-attributed seconds per test class: the class span (first "
                       "startTime to last endTime) plus the idle time before its first test starts while no "
                       "other class in the same TRX was running, which is where shared class fixtures run. "
                       "Each TRX then sums to about its test wall time. Dedicated-lane fixtures are excluded. "
                       "Unmeasured prefixes use one second per test class.",
}


def dedicated(name):
    # Dedicated-lane fixtures never run in a shard, so their time must not weigh one.
    return any(fixture.casefold() in name.casefold()
               for fixture in DEDICATED_FIXTURES + (BLUEPRINT_TEMPLATES_FIXTURE,))


def project_trx(directory, sharding):
    """The TRX files of one project; Conformance also has Rendering namespaces, so identify the assembly."""
    found = []
    for path in sorted(Path(directory).rglob("*.trx")):
        root = ET.parse(path).getroot()
        methods = root.findall("t:TestDefinitions/t:UnitTest/t:TestMethod", TRX_NAMESPACE)
        if methods and all(method.get("codeBase", "").replace("\\", "/").endswith("/" + sharding.assembly)
                           for method in methods):
            found.append((path, root))
    return found


def run_timings(directory, groups, sharding=RENDERING_SHARDING):
    totals = Counter()
    files = project_trx(directory, sharding)
    for path, root in files:
        require_executed(path)
        if any(result.get("outcome") not in ("Passed", "NotExecuted")
               for result in root.findall("t:Results/t:UnitTestResult", TRX_NAMESPACE)):
            raise SystemExit(f"{path}: refresh timings only from successful runs.")
        for name, seconds in MEASURES[sharding.measure](root).items():
            prefix = next((group for group in groups
                           if (name + ".").casefold().startswith(group.casefold())), None)
            if prefix is not None and not dedicated(name):
                totals[prefix] += seconds
    if len(files) != sharding.shards or not totals:
        raise SystemExit(f"{directory}: expected all {sharding.shards} {sharding.name} TRX files, got {len(files)}.")
    return totals


def refresh_timings(directory):
    runs = sorted(path for path in Path(directory).iterdir() if path.is_dir())
    data = json.loads(TIMINGS.read_text()) if TIMINGS.exists() else {}
    refreshed = []
    for sharding in SHARDED:
        # A run without this project's TRX files keeps nothing from it; one with them must be complete.
        project_runs = [run for run in runs if project_trx(run, sharding)]
        if not project_runs:
            continue
        groups = source_groups(sharding)
        observations = [run_timings(run, groups, sharding) for run in project_runs]
        prefixes = sorted(set().union(*(run.keys() for run in observations)))
        measured = {prefix: round(sum(run[prefix] for run in observations if prefix in run) /
                                  sum(prefix in run for run in observations), 6) for prefix in prefixes}
        section = {"measurement": MEASUREMENTS[sharding.measure],
                   "runs": [run.name for run in project_runs], "prefix_seconds": measured}
        if sharding.name == RENDERING_SHARDING.name:
            data.update(section)
        else:
            data[sharding.name] = section
        refreshed.append(f"{len(measured)} {sharding.name} prefixes from {len(project_runs)} runs")
    if not refreshed:
        raise SystemExit("Provide one directory per successful run, each containing every TRX file of a "
                         "sharded project (four Rendering, three Conformance).")
    TIMINGS.write_text(json.dumps(data, indent=2) + "\n")
    print(f"Wrote {'; '.join(refreshed)} to {TIMINGS}.")


def estimate():
    units = {"class-span": "class-span seconds (not parallel job wall time)",
             "wall-attributed": "wall-attributed seconds (about the shard's test wall time)"}
    for sharding in SHARDED:
        prefix_weights = weights(sharding, source_groups(sharding))
        loads = [sum(prefix_weights[prefix] for prefix in part) for part in partitions(sharding)]
        for name, seconds in zip(shard_names(sharding), loads):
            print(f"{name}: {seconds:.1f} {units[sharding.measure]}")
        print(f"{sharding.name} spread: {max(loads) - min(loads):.1f} seconds")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("matrix", "guard", "run", "require-executed", "refresh-timings", "estimate"))
    parser.add_argument("argument", nargs="?")
    parser.add_argument("--blueprint-templates-lane", action="store_true",
                        help="Exclude blueprint template specs only when their dedicated lane runs")
    args = parser.parse_args()
    if args.command in ("run", "require-executed", "refresh-timings") and args.argument is None:
        parser.error(args.command + " requires a shard name/JSON entry or TRX path")
    if args.command == "refresh-timings":
        refresh_timings(args.argument)
        return
    if args.command == "estimate":
        estimate()
        return
    if args.command == "require-executed":
        require_executed(args.argument)
        return
    shards = matrix(blueprint_templates_lane=args.blueprint_templates_lane)
    if args.command == "matrix":
        print(json.dumps(shards))
    elif args.command == "guard":
        guard_solution(shards)
        with open(os.environ["GITHUB_OUTPUT"], "a") as output:
            output.write("matrix=" + json.dumps(shards) + "\n")
    else:
        # CI passes the exact guarded matrix entry; local runs can select it by stable name.
        shard = (json.loads(args.argument) if args.argument.startswith("{") else
                 next(shard for shard in shards["include"] if shard["name"] == args.argument))
        run_shard(shard)


if __name__ == "__main__":
    main()
