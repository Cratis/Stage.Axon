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

Axon uses warning-based degradation by default: unsupported constructs produce intent comments and `UNSUPPORTED.md` entries, with stubs or omitted behavior. Dependent modeled tests are explicitly disabled and listed in `stage-specifications.txt`. `AxonRendering.CreateProfile(strict: true)` makes unsupported constructs errors and refusals instead. A successful default-mode plan therefore requires inspection of its diagnostics and disabled specifications.

For an emitted Maven application, run its wrapper from the output directory:

```bash
sh mvnw -B verify
```

Then pass `target/surefire-reports` to the native-result check. A green Maven run alone does not prove that every modeled specification ran; the manifest correlation detects missing coverage.

### Current profile guarantees

The default scaffold uses in-memory storage. The emitted `persistent` Spring profile provides an aggregate-based JPA event engine and a JPA uniqueness store backed by file-based H2. This is an event/uniqueness runtime, not complete persistence for every generated application: read-model stores remain volatile in every profile, and processor tokens and projector indexes have separate boundaries.

| Concern | Default in-memory application | Persistent profile and limits |
| --- | --- | --- |
| Event-source position | Commands load a per-source `@EventSourcedEntity` through `ManagedEntity` before appending, including repeated commands on the same source. | The same loaded-position path uses the aggregate JPA engine; blind repeated appends are not equivalent. |
| Event tags | Each event has one primary event-source tag. Additional modeled tags produce an explicit degradation warning. | The aggregate-based JPA engine accepts at most one tag per event. |
| Protected decisions | Required presence/removal reads can fold one admitted projection inside the command's own primary stream. Unsupported shapes fail closed with `STAGE-AXON-0106`; asynchronous snapshots are not substituted. | The protected read and append use that stream boundary. Multi-tag or cross-stream protection is not promised. |
| Unique constraints | Supported property-based constraints reserve claims during append and clean up reservations on rollback. Unsupported constraint shapes fail closed with `STAGE-AXON-0801`. | JPA claims, releases and event append share the transaction. Native race/rollback fixtures exercise both profiles; this is not arbitrary-query uniqueness. |
| Reactions | Supported reactions invoke commands or append one typed event. Delivery is at least once, not exactly once; reaction handlers disallow replay. | Storage does not provide exactly-once side effects or durable processor checkpoints. |
| Time | Injected `java.time.Clock`; UTC calendar guards and process-local Spring scheduling. Interval alignment/durability differs explicitly with `STAGE-AXON-0507`. | No durable scheduler guarantee. |
| Restart | Events and default stores do not survive process restart. | An opt-in two-boot H2 probe exercises event and uniqueness-claim survival, including rejection of a second owner without another append. Select `PersistentRestartProbe` with `-Dstage.restart=true`; add `-Dstage.expectViews=true` to require generated view-store beans during both persistent-profile boots. The probe is outside default Surefire discovery, not a disabled default test. It does not prove durable processor tokens, read-model rows or join indexes. |
| Read models | Qualified in-memory stores remain active in every profile, including `persistent`. | This explicit closest equivalent emits a `STAGE-AXON-0308` Warning, an intent-ledger entry and a generated difference comment. View rows do not survive restart: replay is required. No JPA read-model store is emitted. |

Protected decision support currently covers required **presence**, not arbitrary field-value reads. Reaction JUnit lowering currently covers unambiguous application-trigger scenarios without prior state, with event assertions and a pinned clock. Other fixtures stay explicitly disabled rather than succeeding without exercising their behavior. Captures and translations remain unsupported with `STAGE-AXON-0601`; an unauthenticated collection endpoint is not a substitute for source subscription and checkpoint semantics.

Use the same distinctions in your own target: supported behavior, a documented closest equivalent, and an explicitly unsupported construct are three different outcomes. Conformance accounts for them; native specifications establish which implemented behavior actually works. The event/uniqueness fixtures above do not establish that the combined 23-application corpus compiles, boots or passes every modeled specification.

### Measured version pairs

Version admission uses exact language/semantic pairs, not a range:

| Language / semantic | Renderer scenarios covered |
| --- | --- |
| 2.0 / 2.0 | Authored live blueprints and verification samples. |
| 6.0 / 6.0 | Simple event-triggered reactions. |
| 10.0 / 10.0 | Application-triggered reactions and modeled roles. |
| 25.0 / 25.0 | Protected decision reads. |

Admission does not imply support for every feature in that version or a native execution result. Unsupported intents still produce refusals or scaffold gaps. Mixed pairs, unmeasured minor versions and later versions are not admitted.

## Axon state views and queries

The target renders modeled state views as immutable Java records backed by `ReadModelStore`. Axon `@EventHandler` projectors rebuild those records when events arrive. Spring MVC endpoints expose typed snapshot queries and live queries over server-sent events (SSE).

### Projection behavior

Supported forms include flat `from` mappings; `set`, `add`, `subtract`, `increment`, `decrement` and `clear` operations with modeled factors; event properties, literals, event-source identity and supported context values; nested objects and keyed child collections; joins with cached-event backfill; `every` mappings with their child inclusion setting; root, nested, child and join removal; and single-part, named composite and structural parts keys. Records are reconstructed immutably. Removing a nested value does not remove its root; a child join does not apply unrelated parent `every` mappings.

Each generated event has one primary event-source component annotated with Axon's `@EventTag`. A projection's `$eventSourceId` reads that component, not a second identifier.

Join and deferred child indexes live in the projector, not the store. If rows survive a restart, replay events to rebuild these indexes before relying on later join updates. Persisting rows alone is insufficient. Legacy affected-instance cardinality metadata does not select a bulk-update algorithm: the projector applies the event to its modeled key. An external target nomination does not replace the generated Axon projector.

### Query behavior

Generated query records carry typed arguments. Handlers filter the store and enforce modeled cardinality:

| Mode | Generated behavior |
| --- | --- |
| Many | Returns a list of matching read-model records. |
| Zero or one | Returns an optional row; HTTP returns 404 when no row matches. |
| One | Requires exactly one matching row. |
| Snapshot | Exposes a one-shot HTTP GET endpoint. |
| Live | Exposes an HTTP GET returning JSON by default, and `text/event-stream` when the client asks for it. |

Paths use `/api/<module>/<feature-path>/<query-name>` with kebab-case segments. A live endpoint sends an initial snapshot followed by updated query results containing rows, not an Axon transport envelope.

Live queries use Axon subscription queries. Projector invalidations cause the publisher to re-run the original query, so a filtered query does not receive an unfiltered store snapshot. The publisher observes demand, cancels in-flight work and releases its subscription registration. Spring's `SseEmitter` bridges it to HTTP. A session tracker handles `ContextClosedEvent`, cancelling subscriptions and completing emitters before graceful web-server shutdown.

Endpoints and handlers share the renderer's Spring Security mapping. Unsupported authorization policies fail closed with a warning and intent comment; a query is not made public to accommodate an unsupported policy.

### Performed queries and reducers

A query performer becomes a `<Query>Performer` interface taking the typed query and store and returning rows. Handlers delegate to that interface rather than substituting an ordinary store listing. Supply a Spring bean; the default uses `@ConditionalOnMissingBean` and throws `UnsupportedOperationException` until replaced. Inline or attached non-Java implementation bodies are not translated. This path emits `STAGE-AXON-0401` and preserves the modeled intent.

A reducer becomes an event projector delegating to a `<ReadModel>Reducer` interface with typed `reduce(event, current, message)` overloads. The projector stores the returned state and removes the row for `null`. Supply the reduction algorithm as a Spring bean; the default transitions throw. A missing body is not a no-op, and an event/result type does not specify how to calculate state or when to delete it. A reducer without transitions supplies no observed event or algorithm. This closest-equivalent path emits `STAGE-AXON-0306`; dependent specifications remain explicitly disabled.

### Degradation and generated specifications

Unsupported mappings, keys, context values and query shapes produce diagnostics. Closest equivalents carry `// Differs from Screenplay: ...` comments. Unavailable behavior carries `// Screenplay wanted to ...` comments naming its code and semantic path. Strict mode refuses unsupported behavior rather than accepting warning-based scaffolding.

Generated view JUnit tests replay supported given events and assert rows, including keyed absence, composite keys, nested values and child arrays. Unsupported seeded, action-based, exact or query expectations are `@Disabled` with `STAGE-AXON-0501`, not empty passing tests; reducer-dependent specifications use `STAGE-AXON-0306`.

Additional native tests probe subscription initial/update/cancellation behavior and real HTTP SSE initial/update behavior when an unfiltered, many-row live projection query has a usable event fixture. Supported projection specifications with an unfiltered, unauthenticated snapshot-list query also register MockMvc scenarios: given events seed the actual projector, and endpoint JSON is decoded with the application's concept-aware mapper and checked against modeled properties. Specifications without such a query retain their primary view tests; no routes or caller roles are invented. These probes supplement, rather than replace, modeled specification registrations.
