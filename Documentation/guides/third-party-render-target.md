---
title: Verify a third-party render target
description: Check semantic coverage, deterministic output, and native specifications when building a Stage renderer, using Axon as an example.
---

A generated application can compile while silently omitting a modeled rule or specification. Use `Cratis.Stage.Rendering.Conformance` to find those omissions without coupling your renderer to the Cratis target.

Start with [Build a renderer target](build-renderer-target.md) for compilation, profiles, scopes, and publication. This guide assumes you already have an `IArtifactRenderPlanner`, an admitted `ArtifactRenderRequest`, and a native test runner for the generated application.

## Account for every semantic identity

Reference `Cratis.Stage.Rendering.Conformance` from your renderer verification project. The package depends on Stage Contracts and Screenplay, not on a particular renderer or generated runtime.

For each selected scope:

1. Attach the originating semantic identities to `PlannedArtifact.Sources`.
2. Attach a diagnostic to each unsupported identity. Do not account for an entire unsupported subtree with only an application-wide warning.
3. Keep a target-owned registry of diagnostic codes and document their meaning and recovery.
4. Call `RenderCoverage.Check(request, plan, registeredCodes)`.

A clean coverage report means every reachable identity has an artifact, diagnostic, or refusal. It does **not** mean every identity is implemented: a warning accounts for an unsupported construct. The check also rejects unregistered diagnostic/refusal codes and identities that are both realized and refused. Code comparison is ordinal and case-sensitive.

Keep admission policy separate from this accounting. An invalid request remains invalid even if every identity is accounted for. Do not turn a security, privacy, or concurrency requirement into an informational warning that allows unprotected execution.

## Compare repeated plans

Call `RenderDeterminism.Check(planner, request)` with the same immutable request. It plans twice and compares artifact paths, kinds, actual bytes, semantic sources, and typed plan metadata, including diagnostics, refusals, implementation gaps, and required removals.

Make scaffold inputs explicit. If your target accepts a clock or generation seed, fix it for the check. Do not normalize away differences caused by the current time, randomness, filesystem enumeration, or machine-specific paths.

Deterministic output is not behavioral correctness. Continue with the generated application's own compiler and test runner.

## Correlate native tests with modeled specifications

Give every modeled specification one native test identity and one entry in `stage-specifications.txt`. Each entry is tab-separated:

```text
sem1:specification-identity	application.tests.OrderSpecifications#places_order
sem1:unsupported-specification-identity	disabled ACME-RENDER-002	application.tests.OrderSpecifications#unsupported_case
```

These identities are illustrative; write the actual `SemanticId` values from your model. A disabled entry needs a registered target diagnostic code and an explicitly disabled native test, not a passing placeholder.

For JUnit, put the semantic identity in the test's display name. `JUnitXmlResults.ReadDirectory` accepts that identity directly or through a JUnit `display-name` property. When the runner writes only native class/method names, place `stage-specifications.txt` in an ancestor of the result directory so the reader can resolve the mapping.

The following helper verifies a **whole-application** request after the native test runner has completed successfully. Supply your target's complete diagnostic registry, not merely the codes observed in this run:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Conformance;

static void CheckApplication(
    IArtifactRenderPlanner planner,
    ArtifactRenderRequest request,
    IReadOnlySet<string> registeredCodes,
    string outputDirectory,
    string junitDirectory)
{
    var plan = planner.Plan(request);
    if (!plan.Success)
    {
        throw new InvalidOperationException("The render plan was not successful.");
    }

    var manifest = StageSpecificationManifest.Read(
        Path.Combine(outputDirectory, "stage-specifications.txt"));
    var results = JUnitXmlResults.ReadDirectory(junitDirectory);
    CoverageReport[] reports =
    [
        RenderCoverage.Check(request, plan, registeredCodes),
        RenderDeterminism.Check(planner, request),
        SpecificationCoverage.Check(request.Model, manifest, results, registeredCodes)
    ];
    foreach (var report in reports)
    {
        foreach (var issue in report.Issues)
        {
            throw new InvalidOperationException(
                $"{issue.Code}: {issue.SemanticId}: {issue.Message}");
        }
    }
}
```

`SpecificationCoverage` checks all modeled specifications, not just a selected render scope. Every enabled specification needs exactly one passing correlated result. Missing, duplicate, failed, errored, and skipped enabled tests are violations. A registered disabled entry is accounted for, **never counted as a pass**. Inspect disabled entries separately before accepting a target for your application.

## Worked example: Axon

The experimental Axon target emits Java records, Spring components, Axon command handlers, and JUnit tests. Create its profile through `AxonRendering.CreateProfile()` and plan through `AxonArtifactRenderPlanner`; do not depend on its internal emitters as extension APIs.

Axon uses warning-based degradation by default: unsupported constructs produce intent comments and `UNSUPPORTED.md` entries, with compiling stubs or omitted behavior. Dependent modeled tests are explicitly disabled and listed in `stage-specifications.txt`. `AxonRendering.CreateProfile(strict: true)` makes unsupported constructs errors and refusals instead. A successful default-mode plan therefore requires inspection of its diagnostics and disabled specifications.

For an emitted Maven application, run its wrapper from the output directory:

```bash
sh mvnw -B verify
```

Then pass `target/surefire-reports` to the native-result check. A green Maven run alone does not prove that every modeled specification ran; the manifest correlation detects missing coverage.

### Current profile guarantees

The default scaffold uses in-memory storage. A `persistent` Spring profile is not emitted yet; activating a profile name alone does not provide persistence. The persistent-engine column below states the boundary an implementation must preserve, not an available deployment option.

| Concern | Default in-memory application | Persistent-engine boundary |
| --- | --- | --- |
| Event-source position | Commands load a per-source `@EventSourcedEntity` through `ManagedEntity` before appending, including repeated commands on the same source. | Must retain the loaded stream position; blind repeated appends are not equivalent. |
| Event tags | Each event has one primary event-source tag. Additional modeled tags produce an explicit degradation warning. | The OSS aggregate-based JPA engine accepts at most one tag per event. |
| Protected decisions | Required presence/removal reads can fold one admitted projection inside the command's own primary stream. Unsupported shapes fail closed with `STAGE-AXON-0106`; asynchronous snapshots are not substituted. | Must enlist the read and append in the same stream boundary. Multi-tag or cross-stream protection is not promised. |
| Unique constraints | Constrained commands fail closed with `STAGE-AXON-0801`. Atomic uniqueness and typed conflict responses are not implemented. | Claims, releases, and event append must share one database transaction; an independent store transaction is insufficient. |
| Reactions | Supported reactions invoke commands or append one typed event. Delivery is at least once, not exactly once; reaction handlers disallow replay. | Durable processors still need idempotent side effects; storage does not imply exactly-once delivery. |
| Time | Injected `java.time.Clock`; UTC calendar guards and process-local Spring scheduling. Interval alignment/durability differs explicitly with `STAGE-AXON-0507`. | No durable scheduler guarantee. |
| Restart | Events and default stores do not survive process restart. | H2 file storage in DEFAULT mode, shared transactions, token persistence, and restart-safe projection behavior remain required work. |

Protected decision support currently covers required **presence**, not arbitrary field-value reads. Reaction JUnit lowering currently covers unambiguous application-trigger scenarios without prior state, with event assertions and a pinned clock. Other fixtures stay explicitly disabled rather than succeeding without exercising their behavior. Captures and translations remain unsupported with `STAGE-AXON-0601`; an unauthenticated collection endpoint is not a substitute for source subscription and checkpoint semantics.

Use the same distinctions in your own target: supported behavior, a documented closest equivalent, and an explicitly unsupported construct are three different outcomes. Conformance accounts for them; native specifications establish which implemented behavior actually works.
