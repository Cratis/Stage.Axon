#!/usr/bin/env python3
# Copyright (c) Cratis. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

"""Build the spec matrix, guard solution coverage, and reject empty test runs.

Refresh measured prefix weights from directories of successful runs:
    python3 .github/spec-shards.py refresh-timings .ai-work/318/trx
Each immediate child of the TRX root is one run, containing all four Rendering
artifacts. A prefix weighs the summed wall-clock span of its test classes: xUnit's
per-test duration omits the Establish/Because a class shares, so it cannot be used.
`estimate` reports summed class spans, not parallel job wall time.
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
PROJECT_SHARDS = {
    "small-projects": [
        "Source/Contracts/Contracts.csproj",
        "Source/Stage/Stage.csproj",
        "Source/SpecRunner/SpecRunner.csproj",
        "Source/Rendering.Cratis.Scaffolding/Rendering.Cratis.Scaffolding.csproj",
    ],
    "host": ["Source/Host/Host.csproj"],
    # Measured class-span seconds per run: Specifications ~168 (one namespace), Conformance.Specs ~178.
    # The two projects already balance, so a project split is exact and cannot skip or double-run a test.
    "specifications": ["Source/Specifications/Specifications.csproj"],
    "conformance": ["Source/Conformance.Specs/Conformance.Specs.csproj"],
}


def source_groups():
    """Count spec source files; split the large planner namespace by test class."""
    groups = Counter()
    source_root = ROOT / "Source/Rendering.Cratis"
    for source in sorted(source_root.rglob("*.cs")):
        if {"bin", "obj"}.intersection(source.relative_to(source_root).parts):
            continue
        text = source.read_text()
        if not re.search(r"\[(?:Fact|Theory)(?:\(|\])", text):
            continue
        namespace = re.search(r"^namespace ([\w.]+);", text, re.MULTILINE)
        declaration = re.search(r"public (?:sealed |abstract |partial )*class (\w+)", text)
        if namespace is None or declaration is None:
            continue  # Unrecognized/new source shapes still run in the complement shard.
        name = namespace[1]
        if name.startswith(PLANNER):
            prefix = name + "." + declaration[1] + "."
        else:
            # Keep each smaller for_* namespace (including its nested contexts) together.
            parts = name.split(".")
            end = next((i + 1 for i, part in enumerate(parts) if part.startswith("for_")), len(parts))
            prefix = ".".join(parts[:end]) + "."
        groups[prefix] += 1
    return merge_nested_groups(groups)


def merge_nested_groups(groups):
    """One prefix owns nested namespaces/classes, so positive filters cannot overlap."""
    merged = Counter()
    for prefix in sorted(groups, key=lambda name: (len(name), name)):
        parent = next((name for name in merged if prefix.casefold().startswith(name.casefold())), prefix)
        merged[parent] += groups[prefix]
    return merged


def rendering_weights(groups):
    measured = json.loads(TIMINGS.read_text())["prefix_seconds"]
    if any(not isinstance(value, (int, float)) or not math.isfinite(value) or value < 0
           for value in measured.values()):
        raise SystemExit("Spec shard timings must contain finite, non-negative seconds.")
    # One second per source file for unmeasured/new prefixes; discovery still owns coverage.
    return {prefix: measured.get(prefix, count) for prefix, count in groups.items()}


def rendering_partitions():
    weights = rendering_weights(source_groups())
    partitions = [[] for _ in range(RENDERING_SHARDS)]
    loads = [0.0] * RENDERING_SHARDS
    for prefix in sorted(weights, key=lambda name: (-weights[name], name)):
        index = min(range(RENDERING_SHARDS), key=lambda i: (loads[i], i))
        partitions[index].append(prefix)
        loads[index] += weights[prefix]
    if any(not partition for partition in partitions):
        raise SystemExit("Rendering spec discovery must populate all four partitions.")
    return partitions


def matrix(blueprint_templates_lane=False):
    exclusions = EXCLUSIONS
    if blueprint_templates_lane:
        exclusions += "&FullyQualifiedName!~" + BLUEPRINT_TEMPLATES_FIXTURE
    shards = [{"name": name, "projects": projects, "filter": exclusions}
              for name, projects in PROJECT_SHARDS.items()]
    partitions = rendering_partitions()
    for index, prefixes in enumerate(partitions[:-1], start=1):
        selected = "|".join("FullyQualifiedName~" + prefix for prefix in prefixes)
        shards.append({"name": f"rendering-{index}", "projects": [RENDERING],
                       "filter": exclusions + "&(" + selected + ")"})
    # Negate EVERY positive prefix, not a snapshot of the remaining tests. New/unrecognized
    # namespaces and classes necessarily land here, even if discovery never sees their source.
    remainder = "&".join("FullyQualifiedName!~" + prefix
                         for partition in partitions[:-1] for prefix in partition)
    shards.append({"name": "rendering-remainder", "projects": [RENDERING],
                   "filter": exclusions + "&" + remainder})
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
    duplicates = {project: count for project, count in assigned.items()
                  if count != (RENDERING_SHARDS if project == RENDERING else 1)}
    if missing or extra or duplicates or not test_projects:
        raise SystemExit(f"Invalid spec coverage: missing={sorted(missing)}, extra={sorted(extra)}, counts={duplicates}")
    print(f"All {len(test_projects)} solution test projects covered; Rendering has a disjoint complement partition.")


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


def class_spans(root):
    """Wall-clock seconds from a class's first test start to its last test end.

    A shared-lifecycle class runs Establish/Because once, and xUnit leaves that time out of every
    per-test duration. The span keeps it. Concurrent classes overlap, so spans sum to work, not wall time.
    """
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
    return {name: (last - first).total_seconds() for name, (first, last) in bounds.items()}


def dedicated(name):
    # Dedicated-lane fixtures never run in a shard, so their time must not weigh one.
    return any(fixture.casefold() in name.casefold()
               for fixture in DEDICATED_FIXTURES + (BLUEPRINT_TEMPLATES_FIXTURE,))


def run_timings(directory, groups):
    totals = Counter()
    rendering_files = 0
    for path in sorted(directory.rglob("*.trx")):
        root = ET.parse(path).getroot()
        methods = root.findall("t:TestDefinitions/t:UnitTest/t:TestMethod", TRX_NAMESPACE)
        if not methods or not all(method.get("codeBase", "").replace("\\", "/").endswith(
                "/Cratis.Stage.Rendering.Cratis.dll") for method in methods):
            continue  # Conformance also has Rendering namespaces; identify the assembly, not the name.
        rendering = root.findall("t:Results/t:UnitTestResult", TRX_NAMESPACE)
        require_executed(path)
        if any(result.get("outcome") not in ("Passed", "NotExecuted") for result in rendering):
            raise SystemExit(f"{path}: refresh timings only from successful runs.")
        rendering_files += 1
        for name, seconds in class_spans(root).items():
            prefix = next((group for group in groups
                           if (name + ".").casefold().startswith(group.casefold())), None)
            if prefix is not None and not dedicated(name):
                totals[prefix] += seconds
    if rendering_files != RENDERING_SHARDS or not totals:
        raise SystemExit(f"{directory}: expected all four Rendering TRX files, got {rendering_files}.")
    return totals


def refresh_timings(directory):
    groups = source_groups()
    runs = sorted(path for path in Path(directory).iterdir() if path.is_dir())
    if not runs:
        raise SystemExit("Provide one directory per successful run, each containing four Rendering TRX files.")
    observations = [run_timings(run, groups) for run in runs]
    prefixes = sorted(set().union(*(run.keys() for run in observations)))
    measured = {prefix: round(sum(run[prefix] for run in observations if prefix in run) /
                              sum(prefix in run for run in observations), 6) for prefix in prefixes}
    data = {"measurement": "Mean per-run sum of test-class wall-clock spans (first startTime to last endTime) in "
                           "seconds; includes shared class setup. Dedicated-lane fixtures are excluded. Concurrent "
                           "classes overlap, so this is test work, not job wall time. Unmeasured prefixes use one "
                           "second per source file.",
            "runs": [run.name for run in runs], "prefix_seconds": measured}
    TIMINGS.write_text(json.dumps(data, indent=2) + "\n")
    print(f"Wrote {len(measured)} measured prefixes from {len(runs)} runs to {TIMINGS}.")


def estimate():
    weights = rendering_weights(source_groups())
    loads = [sum(weights[prefix] for prefix in partition) for partition in rendering_partitions()]
    for name, seconds in zip(("rendering-1", "rendering-2", "rendering-3", "rendering-remainder"), loads):
        print(f"{name}: {seconds:.1f} class-span seconds (not parallel job wall time)")
    print(f"Spread: {max(loads) - min(loads):.1f} class-span seconds")


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
