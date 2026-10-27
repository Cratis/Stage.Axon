---
title: Verify a third-party render target
description: Check semantic coverage, deterministic output, and native specifications when building a Stage renderer, using Axon as an example.
---

Preview: the conformance API may change ([conformance README](../../Source/Rendering.Conformance/README.md)).

A generated application can compile while silently omitting a modeled rule or specification. Use `Cratis.Stage.Rendering.Conformance` to find those omissions without coupling your renderer to the Cratis target.

Start with [Build a renderer target](https://github.com/Cratis/Stage/blob/v4.197.0/Documentation/guides/build-renderer-target.md), the Stage version matching the pinned Contracts, for compilation, profiles, scopes, and publication. This guide assumes you already have an `IArtifactRenderPlanner`, an admitted `ArtifactRenderRequest`, and a native test runner for the generated application.

## Account for every semantic identity

`Cratis.Stage.Rendering.Conformance` is a preview API; this repository has no package publication configured. Registry availability has not been verified. It depends directly on `Cratis.Stage.Contracts` and transitively on Screenplay, not on a particular renderer or generated runtime. Consume it from a pinned checkout:

1. Install the .NET 10 SDK and target `net10.0` in your verification project.
2. Add Stage.Axon as a submodule: `git submodule add https://github.com/Cratis/Stage.Axon external/Stage.Axon`. Check out a specific commit with `git -C external/Stage.Axon checkout <sha>` and commit the submodule pointer. A clone pinned to the same SHA also works. Pin a SHA, not a branch, because Conformance is a preview API.
3. Match the Contracts version in that checkout's `Directory.Packages.props` (currently `4.197.0`). Without central package management, add:

   ```xml
   <PackageReference Include="Cratis.Stage.Contracts" Version="4.197.0" />
   ```

   With central package management, put `<PackageVersion Include="Cratis.Stage.Contracts" Version="4.197.0" />` in your `Directory.Packages.props` and omit the version from the package reference. Compatibility with a higher Contracts version is not verified.
4. Reference Conformance in Release, adjusting the relative path for your project:

   ```xml
   <ProjectReference Include="../external/Stage.Axon/Source/Rendering.Conformance/Rendering.Conformance.csproj"
                     SetConfiguration="Configuration=Release" />
   ```

   This metadata selects Release even when your consumer builds in Debug. Conformance opts out of inline specifications and test dependencies in both configurations; its specifications live in the separate `Rendering.Conformance.Specs` project.

Do not copy the project folder alone: it inherits its target framework, package versions, analyzers, and specification configuration from repository-level build files. Keep the pinned checkout intact so MSBuild evaluates the referenced project with its own configuration. When updating the pin, re-read its Contracts version and rebuild.

### Run the minimal target

From the pinned checkout root, run the standalone [minimal target](../../Source/Samples.MinimalTarget/Program.cs):

```sh
dotnet restore Source/Samples.MinimalTarget/Samples.MinimalTarget.csproj -p:Configuration=Debug
dotnet run --project Source/Samples.MinimalTarget/Samples.MinimalTarget.csproj -c Debug --no-restore
```

It prints `# Sample` followed by a newline and exits successfully only when planning, semantic coverage, and repeated-plan determinism pass. It constructs an empty application and plans one UTF-8 Markdown artifact, `application.md`, in memory; it writes no generated files and needs no Java runtime or Maven.

The internal planner demonstrates only the existing public Contracts API. It accepts the exact model application's scope with initialized, empty `AdditionalScopes`, and rejects foreign application identities, non-application scopes, and default or nonempty additional scopes before creating an artifact. Its only semantic source is the application root: it does not implement or claim coverage for modules, slices, or other nested constructs. Adding such constructs requires a real lowering or an attributed diagnostic; the empty fixture's clean coverage is not evidence of subtree support.

For your own target, apply the following checks to each selected scope:

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
    var issues = new List<string>();
    var plan = planner.Plan(request);
    if (!plan.Success)
    {
        issues.Add("The render plan was not successful.");
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
            issues.Add($"{issue.Code}: {issue.SemanticId}: {issue.Message}");
        }
    }
    if (issues.Count > 0)
    {
        throw new InvalidOperationException(string.Join(Environment.NewLine, issues));
    }
}
```

`SpecificationCoverage` checks all modeled specifications, not just a selected render scope. Every enabled specification needs exactly one passing correlated result. Missing, duplicate, failed, errored, and skipped enabled tests are violations. A registered disabled entry is accounted for, **never counted as a pass**. Inspect disabled entries separately before accepting a target for your application.

## Worked example: Axon

The experimental Axon target emits Java records, Spring components, Axon command handlers, and JUnit tests. Create its profile through `AxonRendering.CreateProfile()` and plan through `AxonArtifactRenderPlanner`; do not depend on its internal emitters as extension APIs.

Generated files have no license header by default. To supply your own header for Java sources and tests, use `AxonRendering.CreateProfile(fileHeader: ["Copyright (c) Example", "All rights reserved."])`. Each plain-text line becomes a `//` comment, followed by a blank line. Header lines must be non-null and contain no line breaks. Resources and build files do not receive this optional header; the header is part of the hashed render profile.

Generated applications use `org.axonframework.extensions.spring:axon-spring-boot-starter`, managed by `org.axonframework:axon-framework-bom:5.4.1`, with `axon-spring-boot-starter-test` from the same Spring extension group for tests. This starter bundles Spring integration and Axon messaging, modeling, and event sourcing without an Axon Server connector; generated applications do not require Axon Server.

Axon uses warning-based degradation by default: unsupported constructs produce intent comments and `UNSUPPORTED.md` entries, with stubs or omitted behavior. Dependent modeled tests are explicitly disabled and listed in `stage-specifications.txt`. `AxonRendering.CreateProfile(strict: true)` makes unsupported constructs errors and refusals instead. A successful default-mode plan therefore requires inspection of its diagnostics and disabled specifications.

For an emitted Maven application, run its wrapper from the output directory:

```bash
sh mvnw -B verify
```

Then pass `target/surefire-reports` to the native-result check. A green Maven run alone does not prove that every modeled specification ran; the manifest correlation detects missing coverage.

### Current profile guarantees

The default scaffold uses in-memory storage. The emitted `persistent` Spring profile provides an aggregate-based JPA event engine, a JPA uniqueness store and flat projection view rows backed by file-based H2. Scoped and reducer-built views are refused with `STAGE-AXON-0308`. A legacy database without a view generation, or with a mismatched generation, fails closed before processors or readiness; automatic replay is not a migration strategy. Persistent flat views require pooled processors, propagating errors and context-bound writes on the handler thread. Startup validates the resolved processor envelope after customizations. Runtime row/checkpoint atomicity is proven only by the CI Maven gate; C# source assertions do not prove transaction behavior.

| Concern | Default in-memory application | Persistent profile and limits |
| --- | --- | --- |
| Event-source position | Existing-identity commands inject the per-source `@EventSourcedEntity` as an `@Nullable Stream` snapshot with `@InjectEntity` before appending, allowing first appends when the stream has no events. Required decision reads reject absent state; neither `Optional<Stream>` nor `ManagedEntity<Id, Stream>` is injected. Generated identities still load through `StateManager.loadManagedEntity`. Single unconditional, unconstrained events append directly; conditional, multiple and constrained events append as a batch, including repeated commands on the same source. | The same loaded-position path uses the aggregate JPA engine; blind repeated appends are not equivalent. |
| Event tags | Each event has one primary event-source tag. Its owning module exposes the key through `events.EventTags` constants, shared by event annotations, stream state and entity injection without changing stored tag values. Additional modeled tags produce an explicit degradation warning. | The aggregate-based JPA engine accepts at most one tag per event. |
| Protected decisions | Required presence/removal reads can fold one admitted projection inside the command's own primary stream. Unsupported shapes fail closed with `STAGE-AXON-0106`; asynchronous snapshots are not substituted. | The protected read and append use that stream boundary. Multi-tag or cross-stream protection is not promised. |
| Unique constraints | Supported property-based constraints reserve claims during append and clean up reservations on rollback. Unsupported constraint shapes fail closed with `STAGE-AXON-0801`. | JPA claims, releases and event append share the transaction. Native race/rollback fixtures exercise both profiles; this is not arbitrary-query uniqueness. |
| Reactions | Supported reactions invoke commands or append one typed event. Delivery is at least once, not exactly once; reaction handlers disallow replay. | Processor checkpoints may persist independently of volatile view state. Storage does not establish coordinated view/reaction recovery or exactly-once side effects. |
| Time | Injected `java.time.Clock`; UTC calendar guards and process-local Spring scheduling. Interval alignment/durability differs explicitly with `STAGE-AXON-0507`. | No durable scheduler guarantee. |
| Restart | Events and default stores do not survive process restart. | Persistent flat-view fixtures emit two-boot row/checkpoint checks, handler-after-write rollback, real TokenStore persistence failure, handler-thread/non-joined transaction refusal and generation-refusal tests. These require CI Maven execution. The opt-in `PersistentRestartProbe` remains a separate event/uniqueness probe, not proof of coordinated reaction recovery. |
| Read models | Qualified in-memory stores remain unchanged in the default profile. | The persistent profile supports admitted flat projection views with namespaced JPA rows and generation metadata. Scoped and reducer-built views remain refused, including mixed profiles containing `persistent`. Legacy databases without a view generation fail closed. No automatic migration, scoped-index recovery or exactly-once reaction guarantee is supplied. |

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

Join and deferred child indexes live in the projector, not the store, and remain volatile in the default profile. Persistent startup is refused for scoped and reducer-built views and for projections requiring those indexes. Only admitted flat projection rows share the pooled processor transaction with its checkpoint, subject to propagating errors and context-bound handler-thread writes; runtime atomicity requires CI Maven proof. Reaction effects are not covered by that guarantee, and automatic replay is not a migration strategy. Legacy affected-instance cardinality metadata does not select a bulk-update algorithm: the projector applies the event to its modeled key. An external target nomination does not replace the generated Axon projector.

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

A query performer becomes a `<Query>Performer` interface taking the typed query and store and returning rows. Handlers delegate to that interface rather than substituting an ordinary store listing. An admitted inline Java body fills the existing default method; a Spring bean can still replace it through `@ConditionalOnMissingBean`. Without an admitted body the default throws `UnsupportedOperationException`, emits `STAGE-AXON-0401` and preserves the modeled intent. Non-Java bodies are not translated.

A reducer becomes an event projector delegating to a `<ReadModel>Reducer` interface with typed `reduce(event, current, message)` overloads. Admitted inline Java bodies are copied verbatim, not translated from another language. For example, `return new RebuiltTotal(event.id(), (current == null ? 0L : current.amount()) + event.amount());` initializes and accumulates state; `return null;` deletes it. `current` is nullable on first delivery, and `message` is Axon's `EventMessage`. Bodies must be pure by author obligation; Axon does not verify purity. No helper services are injected. Modeled initial state is advisory: authors initialize from null explicitly. A custom Spring bean may still replace the generated default bean.

Unplaced transitions emit `STAGE-AXON-0306` and return `current`: **events are ignored until a supported body is authored**. A null `current` on the first event leaves the view absent; the placeholder neither creates a row nor deletes an existing one. Foreign-language bodies are not executed. Event-target, missing-model, duplicate/missing-event and unsupported key/result reducers are refused; filtered `From` reducers remain unplaced rather than ignoring their filter. A reducer without transitions supplies no observed event or algorithm.

### Target-native Java body contracts

`ArtifactRenderRequest.ImplementationContents` supplies bodies by stable requirement ID. The renderer resolves that exact ID and checks semantic owner/role, context/result revision 1, resolved attachment, ambiguity/provisional flags, supported capability and UTF-8 SHA-256 revision. The compiler dependency is explicitly `Cratis.Screenplay` 4.250.0; this does not migrate Contracts or the canonical corpus.

| Role | Existing native method contract |
| --- | --- |
| Command handler | `handle(Command command, EventAppender appender, ...)` with the modeled return type. Supported generated-value preparation, validation, security and decision guards remain. The admitted body owns effects and returns; modeled appends are not also fabricated. An unavailable required surrounding contract still fails closed. Commands that produce or release a uniqueness-constrained event cannot use a native handler: `STAGE-AXON-0801` emits a warning/comment and throwing stub (strict mode refuses) before preparation or body execution. The modeled path retains unit-of-work constraint reservations/releases; no unverified appender adapter or body containment is invented. |
| Standalone reaction effect | Existing `onN(...)` method, with `input` when the trigger has a payload and `occurred` for an event timestamp. Supported scheduling/where guards and trusted-system identity `try/finally` remain. A native effect cannot also declare invokes/produces; unsupported identity/from combinations are not consumed. |
| Query performer | Existing `default List<Model> perform(Query query, ReadModelStore<Object, Model> store)`. The typed handler delegation and replaceable Spring bean are unchanged. |

These target-native variables are not a universal `context` ABI. No context/result adapter is invented and no authored Java is scanned to infer identities, imports or effects. Use qualified names inside the body for additional Java types. Admitted bodies are copied exactly once without reindentation, reference shortening, synthetic results or trailing fallback throws.

**Transport limit:** only LF-only bodies can be admitted. Published `PlannedArtifact.CreateText` normalizes CR/CRLF, so any body containing CR is rejected before insertion with an attributed warning and intent comment; strict mode records an error/refusal. The renderer neither rewrites that content nor labels Java as a binary artifact. Missing/stale/file-backed content, unknown revisions/capabilities and foreign languages also remain unplaced. After emission, one comment-only `runtime/UnsupportedImplementations.java` records scoped unconsumed command, reaction, performer and reducer requirements. Foreign languages use `STAGE-AXON-IMPL-LANGUAGE`; other unplaced slots use `STAGE-AXON-0403` with their feature diagnostics. Unrelated scopes and already consumed bodies are excluded. Reducer transition slots require the `pure` capability (other slots require `provider-defined`). Unplaced Java and foreign-language reducers retain `STAGE-AXON-0306` degradation and disabled dependent specifications; admitted Java transitions are consumed only when actually emitted.

Generated surrounding code uses short imported type names. Local declarations take precedence; command application types are preclaimed in ordinal qualified-name order before framework annotations. Only colliding references fall back to qualified names. Tag constants import their owning `EventTags` type, and only use an explicit `idProperty` when the command accessor actually has that tag name; otherwise Axon's default destination inference is retained. Annotation names and stored tag values are unchanged.

Generated enums retain their explicit scalar `@JsonValue`/`@JsonCreator` contract (including noncontiguous values), not ordinal serialization. The application and test mapper omit the broad `EnumModule` so it cannot override those annotations. Runtime and HTTP fixtures exercise scalar, nested, concept-wrapped and map-key round trips and invalid-number rejection. C# emitted-text specs do not prove JVM behavior; the existing CI Maven gate remains the native authority.

API references: [Axon event sourcing](https://docs.axoniq.io/axon-framework-reference/5.0/axon-framework-commands/modeling/event-sourcing/), [JSpecify nullable](https://jspecify.dev/docs/api/org/jspecify/annotations/Nullable.html), and [Jackson annotations](https://github.com/FasterXML/jackson-annotations).

### Degradation and generated specifications

Nonempty `ThenInvocations` assertions emit an attributed `STAGE-AXON-SPEC` warning (strict mode refuses): generated tests do not verify those command calls or argument values. The unsupported assertion is retained in `UNSUPPORTED.md`, not silently treated as a successful check.

Unsupported mappings, keys, context values and query shapes produce diagnostics. Closest equivalents carry `// Differs from Screenplay: ...` comments. Unavailable behavior carries `// Screenplay wanted to ...` comments naming its code and semantic path. Strict mode refuses unsupported behavior rather than accepting warning-based scaffolding.

Generated view JUnit tests replay supported given events and assert rows, including keyed absence, composite keys, nested values and child arrays. Unsupported seeded, action-based, exact or query expectations are `@Disabled` with `STAGE-AXON-0501`, not empty passing tests; reducer specifications with unavailable transitions use `STAGE-AXON-0306`. Fully supported reducer specifications replay ordered events through the generated projector, using `AnnotationMessageTypeResolver` and `GenericEventMessage`, then assert modeled rows or keyed absence. Unsupported shape/property/value/key assertions disable the entire specification with `STAGE-AXON-0501`; no supported subset runs. The views corpus covers initialization, accumulation through current and deletion after insertion. Native/Maven proof is pending CI, not established by .NET emission checks.

Additional native tests probe subscription initial/update/cancellation behavior and real HTTP SSE initial/update behavior when an unfiltered, many-row live projection query has a usable event fixture. Supported projection specifications with an unfiltered, unauthenticated snapshot-list query also register MockMvc scenarios: given events seed the actual projector, and endpoint JSON is decoded with the application's concept-aware mapper and checked against modeled properties. Specifications without such a query retain their primary view tests; no routes or caller roles are invented. These probes supplement, rather than replace, modeled specification registrations.
