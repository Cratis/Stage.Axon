# Stage.Axon

The Axon Framework JVM render target for Cratis Stage. It plans Java applications from Screenplay executable semantic models using the published `Cratis.Stage.Contracts` 4.203.0 package. The planner, embedded Java templates, and native verification harness retain their history from Cratis/Stage. This repository does not contain the Stage engine.

`Source/Rendering.Conformance` is a vendored, Contracts-only conformance helper until it is published separately. The verification corpus retains 15 live blueprints, three samples, and four individual view models (also rendered together): 23 applications. Their models remain the behavioral input contract.

The `sample-custom-packages` application composes four retained local documents with six documents from `Cratis.Screenplay.CanonicalCorpus` 4.241.0: the Tracking feature and its five slices (`add-comment-slice`, `close-work-item-slice`, `rename-work-item-slice`, `work-item-comments-slice`, `work-item-details-slice`). Each is selected from `ScreenCompositionCorpus.V1.SourceForms.Single(form => form.Name == "folder")` by its public stable key, and its exact package bytes are written into a disposable input directory; a missing, duplicate, path-mismatched or empty document, or a document that would shadow a local copy, fails without a fallback. Every other corpus input and all 23 application names remain unchanged. The package is a verification dependency only, with no production renderer dependency. [`Corpus/README.md`](Corpus/README.md) records the package-sourced documents and why every other vendored model is retained.

The renderer ships as the experimental NuGet package `Cratis.Stage.Rendering.Axon` (0.x) when PRs labelled `major`/`minor`/`patch` merge to main. Generated apps depend on `io.cratis:stage-axon-runtime` from Maven Central, pinned to the renderer's version and published from the same release. `Cratis.Stage.Contracts` and `Cratis.Screenplay` follow the CLI's `StageVersion` and `ScreenplayVersion` (one-line pins in `Directory.Packages.props`). The required `cli-version-guard.mjs` check runs in build and publish: different majors or pins ahead of CLI main fail; same-major lag warns with a one-line bump. Fetch failures fail closed; `CLI_PACKAGES_PROPS=<path>` supplies an offline CLI fixture. Rendering.Conformance, the verification host and the sample are not published.

## Build and specs

Requires .NET 10. Run from this directory:

```sh
dotnet build Stage.Axon.slnx -c Release -warnaserror
dotnet build Stage.Axon.slnx -c Debug
dotnet test Stage.Axon.slnx -c Debug --no-build
```

Specs are inline in all three projects and are compiled only in Debug. The extracted baseline is 389 planner specs, 16 verification specs, and 33 conformance specs (438 total, no skips). Repository analyzers run during both builds; Release treats warnings as errors.

## Native Java method-body slots

The renderer pins `Cratis.Screenplay` 4.250.0 while retaining Contracts 4.203.0. It can place resolved inline `java` bodies in three existing target-native methods: command handlers (`command`, `appender`, declared return type), standalone reaction effects (`input` and event `occurred` when present), and query performers (`query`, `store`, returning `List<Model>`). Native handlers for commands producing or releasing supported property- or event-occurrence-uniqueness events receive a `ModeledConstraints.enforcing` appender in a private body method. It reserves/releases before each append in the same unit of work; any failure during enforcement or delegated append, even if swallowed, fails prepare-commit. Unsupported constraints and bodies naming another appender, sink, gateway or store, or containing a Java Unicode escape (`\u` sequence), still fail closed with `STAGE-AXON-0801`; the lexical guard is defense in depth, not containment.

Property uniqueness allows the same owner to reassert a value; changing it releases the previous claim in the same unit of work. Event-occurrence uniqueness is non-idempotent per constraint and event-source owner until a `ReleasedBy` event reopens it. The owner is the event's primary tag component (also for release), not an arbitrary event property or the complete tag set. Support requires `EventSequence`, nonempty resolved event targets, no target properties and resolvable primary tags with nonnumeric or integral identities. Non-integral numeric primary tags (including decimal concepts) fail closed with `STAGE-AXON-0801`, avoiding numeric normalization that could merge distinct source identities. Property-uniqueness normalization is unchanged. Commit publishes reservations, rollback discards them, and swallowed conflicts still fail prepare-commit. In-memory reservations reject contenders immediately without retry, including contenders whose holder later rolls back. The `constrained-java-bodies` corpus application exercises property and occurrence uniqueness through native and modeled handlers; native proofs run in CI. These are method slots, **not** a universal context-object ABI. Preparation, validation, supported security and reaction identity cleanup remain around the body; an admitted command body owns effects and returns instead of also executing modeled appends.

- Release note (#64): enforce event-occurrence uniqueness per event source in the append unit of work, with release and rollback support, and native-handler proofs for both in-memory and JPA profiles.

Inline `java` authorization policy bodies are placed natively in separate `infrastructure/<Name>Policy.java` compilation units; `Policies.java` contains only the evaluator. Lexical scope escape in one trusted body cannot shadow the evaluator's decision enum or a sibling policy. Any Java Unicode escape occurrence in a policy body is rejected (including in literals/comments); no brace-balancing heuristic is used. A body sees `Authentication authentication` and `Map<String,Object> claims`, must return `boolean`, and must not redeclare those parameter names. JWT attributes take precedence over details-map claims; Basic callers get `sub`. Demo claims are opt-in under the `demo` profile only (`stage.axon.demo.claims.*`); no profile is activated automatically. Every authorization site involving an inline policy calls one Java evaluator from `@PreAuthorize`. Evaluation is eager and three-valued: any FAILED operand denies through every operator. Otherwise AND requires both ALLOW; OR requires either ALLOW, so a granting role OR a legitimately denying policy grants. Screenplay 4.250.0 rejects inline policies beneath NOT; an upstream-refusal spec pins that restriction, while the evaluator retains defensive FAILED propagation for NOT. Anonymous/unauthenticated callers, unsupported constructs, missing bodies, exceptions and errors never become grants. Sites without inline policies retain their existing mapping. Purity is the author's obligation, not sandbox enforcement. Inline policy bodies are trusted, unsandboxed application code.

Admission requires supported version-1 context/result contracts, matching owner and role, resolved unambiguous non-provisional identity, and content whose UTF-8 SHA-256 matches the requirement. Bodies must be **LF-only**: any CR, including CRLF, is rejected with attributed diagnostics/comments (strict mode refuses), not silently normalized or emitted as binary. Admitted bytes, indentation, literals and qualified names are copied unchanged; use qualified names for extra Java types. File-backed, foreign-language, missing, stale and unsupported-contract bodies remain unplaced. Scoped unplaced requirements are recorded in `runtime/UnsupportedImplementations.java` and `UNSUPPORTED.md`; foreign languages use `STAGE-AXON-IMPL-LANGUAGE` (the warning names the authored language and the remedy; strict mode refuses), other unsupported slots use `STAGE-AXON-0403` alongside their feature diagnostics. Pure inline Java reducer transitions use the same admission checks and are placed verbatim in `reduce(event, current, message)`. Authors own purity; Axon does not verify it. Fully supported reducer view specifications execute generated JUnit tests; unplaced transitions retain `STAGE-AXON-0306` and return `current` (events are ignored until a supported body is authored; null stays absent).

### Slice 2 notes

- Generated command records now use import-aware type references with qualified collision fallback. (#10)
- Command/reaction streams and entity selectors import the owning `EventTags` type, never the constant as a type. (#10)
- Entity injection uses nullable stream snapshots; absent state rejects required decision reads. (part of #3)
- Generated enum JSON retains declared values such as 10/30 through `@JsonValue`/`@JsonCreator`; broad ordinal-module registration is omitted. (part of #3)
- Compiler pinning, native method slots and scoped unplaced-body diagnostics preserve unsupported intent rather than inventing results. (part of #3)

C# emitted-text assertions are not native execution proof. The Java fixtures and all generated applications still require the CI native gate below.

## No persistent profile

There is no `persistent` profile. Event, token, uniqueness-claim and read-model data are held in memory (read models in H2 through JPA) and are lost on restart.

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
