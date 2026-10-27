# Stage.Axon

The Axon Framework JVM render target for Cratis Stage. It plans Java applications from Screenplay executable semantic models using the published `Cratis.Stage.Contracts` 4.203.0 package. The planner, embedded Java templates, and native verification harness retain their history from Cratis/Stage. This repository does not contain the Stage engine.

`Source/Rendering.Conformance` is a vendored, Contracts-only conformance helper until it is published separately. The verification corpus retains 15 live blueprints, three samples, and four individual view models (also rendered together): 23 applications. Their models remain the behavioral input contract.

The `sample-custom-packages` application composes four retained local documents with six documents from `Cratis.Screenplay.CanonicalCorpus` 4.241.0: the Tracking feature and its five slices (`add-comment-slice`, `close-work-item-slice`, `rename-work-item-slice`, `work-item-comments-slice`, `work-item-details-slice`). Each is selected from `ScreenCompositionCorpus.V1.SourceForms.Single(form => form.Name == "folder")` by its public stable key, and its exact package bytes are written into a disposable input directory; a missing, duplicate, path-mismatched or empty document, or a document that would shadow a local copy, fails without a fallback. Every other corpus input and all 23 application names remain unchanged. The package is a verification dependency only, with no production renderer dependency. [`Corpus/README.md`](Corpus/README.md) records the package-sourced documents and why every other vendored model is retained.

The renderer ships as the experimental NuGet package `Cratis.Stage.Rendering.Axon` (0.x) when PRs labelled `major`/`minor`/`patch` merge to main. `Cratis.Stage.Contracts` follows the CLI's `StageVersion` (one line in `Directory.Packages.props`, checked by `cli-stage-version.yml`). Rendering.Conformance, the verification host and the sample are not published.

## Build and specs

Requires .NET 10. Run from this directory:

```sh
dotnet build Stage.Axon.slnx -c Release -warnaserror
dotnet build Stage.Axon.slnx -c Debug
dotnet test Stage.Axon.slnx -c Debug --no-build
```

Specs are inline in all three projects and are compiled only in Debug. The extracted baseline is 389 planner specs, 16 verification specs, and 33 conformance specs (438 total, no skips). Repository analyzers run during both builds; Release treats warnings as errors.

## Native Java method-body slots

The renderer pins `Cratis.Screenplay` 4.250.0 while retaining Contracts 4.203.0. It can place resolved inline `java` bodies in three existing target-native methods: command handlers (`command`, `appender`, declared return type), standalone reaction effects (`input` and event `occurred` when present), and query performers (`query`, `store`, returning `List<Model>`). Native handlers for commands that produce or release a uniqueness-constrained event fail closed with `STAGE-AXON-0801`; their bodies never bypass append-time constraint enforcement. These are method slots, **not** a universal context-object ABI. Preparation, validation, supported security and reaction identity cleanup remain around the body; an admitted command body owns effects and returns instead of also executing modeled appends.

Admission requires supported version-1 context/result contracts, matching owner and role, resolved unambiguous non-provisional identity, and content whose UTF-8 SHA-256 matches the requirement. Bodies must be **LF-only**: any CR, including CRLF, is rejected with attributed diagnostics/comments (strict mode refuses), not silently normalized or emitted as binary. Admitted bytes, indentation, literals and qualified names are copied unchanged; use qualified names for extra Java types. File-backed, foreign-language, missing, stale and unsupported-contract bodies remain unplaced. Scoped unplaced requirements are recorded in `runtime/UnsupportedImplementations.java` and `UNSUPPORTED.md`; foreign languages use `STAGE-AXON-IMPL-LANGUAGE`, other unsupported slots use `STAGE-AXON-0403` alongside their feature diagnostics. Pure inline Java reducer transitions use the same admission checks and are placed verbatim in `reduce(event, current, message)`. Authors own purity; Axon does not verify it. Fully supported reducer view specifications execute generated JUnit tests; unplaced transitions retain `STAGE-AXON-0306` and return `current` (events are ignored until a supported body is authored; null stays absent).

### Slice 2 notes

- Generated command records now use import-aware type references with qualified collision fallback. (#10)
- Command/reaction streams and entity selectors import the owning `EventTags` type, never the constant as a type. (#10)
- Entity injection uses nullable stream snapshots; absent state rejects required decision reads. (part of #3)
- Generated enum JSON retains declared values such as 10/30 through `@JsonValue`/`@JsonCreator`; broad ordinal-module registration is omitted. (part of #3)
- Compiler pinning, native method slots and scoped unplaced-body diagnostics preserve unsupported intent rather than inventing results. (part of #3)

C# emitted-text assertions are not native execution proof. The Java fixtures and all generated applications still require the CI native gate below.

## Persistent flat views

The opt-in `persistent` profile supports flat projection views only, using JPA rows and generation metadata. Scoped and reducer-built views are refused. A legacy database without a matching view generation fails closed; automatic replay is not a migration strategy.

The transaction envelope requires pooled processors with propagating errors and context-bound writes on the handler thread. Startup checks reject customizations that weaken the processor envelope. Runtime row/checkpoint atomicity is proven only by the CI Maven gate, not by C# emitted-text assertions. Default-profile stores remain in memory.

## Native proof

Use JDK 25 and the generated Maven wrapper. The render destination must not exist.

```sh
dotnet run --project Source/Rendering.Axon.Verification -c Debug --no-build -- render .ai-work/corpus
(cd .ai-work/corpus && sh ./mvnw -B -fae verify)
dotnet run --project Source/Rendering.Axon.Verification -c Debug --no-build -- verify .ai-work/corpus
```

The last command requires passing native tests in every application and validates the exact disabled-test budget recorded in [`disabled-tests.json`](Source/Rendering.Axon.Verification/disabled-tests.json). Missing, failing, empty, all-skipped, unknown-reason, exceeded, and stale budgets fail. Do not raise budgets to obtain a passing run. See [the harness guide](Source/Rendering.Axon.Verification/README.md) for optional restart and boot smoke checks and [the target guide](Documentation/guides/third-party-render-target.md) for renderer contracts and Axon limitations.

The build workflow runs Release/Debug builds, all spec projects with nonempty-result guards, package allowlist checks, and the 23-app native proof. It needs no containers or Stage checkout. The publish workflow releases only the renderer package.

Licensed under the [MIT license](LICENSE).
