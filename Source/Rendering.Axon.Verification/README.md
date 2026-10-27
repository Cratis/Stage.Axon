# Axon native verification harness

Run from the repository root after building the solution in Debug:

```bash
dotnet test Source/Rendering.Axon.Verification/Rendering.Axon.Verification.csproj --configuration Debug --no-build
dotnet run --project Source/Rendering.Axon.Verification/Rendering.Axon.Verification.csproj --configuration Debug --no-build -- render .ai-work/axon-corpus
```

The destination must not exist. The harness renders 15 live blueprints, three verification samples, four individual view models, and their combined application. Each compilation and render must succeed without refusals. The output includes a Maven reactor and an application list.

The custom-package sample uses mixed packaged and retained input. `Cratis.Screenplay.CanonicalCorpus` is pinned to 4.241.0; six documents (the Tracking feature and five Tracking slices) are selected from `ScreenCompositionCorpus.V1.SourceForms.Single(form => form.Name == "folder")` by public stable key, at the display paths listed in [`Corpus/README.md`](../../Corpus/README.md). The harness copies the four retained custom-package documents into an owned disposable directory under `.ai-work/axon5/`, adds the six package documents, and keeps that scope alive through compilation and rendering. Disposal removes the composed input tree, including on failure. A missing, duplicate, path-mismatched or empty selection, or a package document that would overwrite a local file, fails before output creation; there is no vendored fallback.

The six covered local documents are removed. All other inputs and all 23 application names are retained without importing the canonical Workspaces application or identity catalog; the reasons are recorded in [`Corpus/README.md`](../../Corpus/README.md). The same input helper and package are linked only into Debug renderer specs; the production renderer does not depend on CanonicalCorpus.

Run the generated Maven wrapper with Java 25, then check the actual Surefire reports:

```bash
(cd .ai-work/axon-corpus && sh ./mvnw -B -fae verify)
dotnet run --project Source/Rendering.Axon.Verification/Rendering.Axon.Verification.csproj --configuration Debug --no-build -- verify .ai-work/axon-corpus
node Source/Rendering.Axon.Verification/boot-smoke.mjs .ai-work/axon-corpus .ai-work/axon-boot-logs
```

Every application must execute passing native tests. Missing, failed, empty, or entirely skipped reports fail verification. `disabled-tests.json` records an exact count and explanation per reason, measured independently from all 23 generated `stage-specifications.txt` files and Java `@Disabled` annotations: the reasons and counts in that file; this is source accounting, not a native execution result. Unknown reasons, increases, decreases, absent reasons and unexplained or zero-count entries fail. Ratchet the allowlist whenever lowering improves; do not raise it merely to obtain a passing run.

The existing four-file views corpus includes native reducer initialization, accumulation and deletion scenarios.

Persistent startup supports admitted flat projections with durable view rows and generation metadata; scoped projections, reducers and other unsupported shapes remain guarded by `STAGE-AXON-0308`. Supported views require pooled processors with propagating error handling. Missing or incompatible generation metadata on retained data is refused: no automatic replay, reset or legacy adoption is performed.

Generated default Surefire proofs cover recovery before new appends, retained checkpoints, subsequent updates, handler/token-store failure rollback and retry, and rejection of off-thread or non-joined writes. Additional cases cover generation refusal, processor customization guards and default-profile handler dispatch/query invalidation. These tests are included in the generated Maven `verify` run used by CI.

The opt-in `PersistentRestartProbe` remains outside default Surefire discovery:

```bash
(cd .ai-work/axon-corpus && sh ./mvnw -B -pl orders -Dtest=PersistentRestartProbe -Dstage.restart=true -Dstage.expectViews=true test)
```

It checks event and uniqueness-claim survival in a viewless fixture. For the selected application, unsupported views must refuse both seeded and fresh databases; supported flat views must refuse seeded legacy data without changing event, claim or token snapshots, but may start on a fresh database with generation metadata. Without `stage.expectViews=true`, successful main-application probes must be viewless. This probe supplements, rather than replaces, the default native tests.

The boot smoke starts each packaged application, checks its intentional root HTTP 404, and stops every process it owns. It does not assert modeled command or query behavior; those assertions belong to the generated native tests. CI should put generated output and logs under `runner.temp`, not in the checkout.

## Ingress and atomicity proofs

The `ingress-invariants` and `ingress-persistent` verification fixtures bring the corpus to 25 applications without changing the original 23 application names. They use the same semantic preparation: a case-insensitive uniqueness constraint, authenticated-caller event mapping, required own-stream protected reads, and an unsupported cross-stream read. They have no disabled specifications; `disabled-tests.json` remains `{}`.

`ingress-invariants` retains its scoped projection and runs the memory and authorization proofs only. It is not started in the persistent profile by the ingress proofs. `ingress-persistent` selects only command slices from the same valid semantic model: protected reads still retain their removal-aware event-stream state, but no view, projector or unsupported persistent view store is emitted. Only this viewless app receives the JPA uniqueness/rollback and persistent protected-read proof subclasses. Semantic validation requires the removals for protected reads; dropping them is not a valid flat-view workaround. The boot smoke expects 25 distinct applications.

Default Surefire discovery runs generated gateway uniqueness races against the actual JPA store, protected-read races in memory and persistent profiles, and HTTP/gateway method-security checks. Every race repeats 25 times with eight contenders, bounded barriers/latches and bounded termination; no sleeps or direct test appends are used. A spy calls the real JPA reservation before forcing a rollback; another rendezvous follows generated decision loading and validation. Tests assert recorded event counts and ownership, not merely returned exceptions. Protected-read losing exception classes are printed by the native tests.

The authorization tests deny anonymous and wrong-role commands before claims or appends, deny snapshot queries on both ingress paths, and accept authorized calls. Recorded facts must identify the authenticated caller rather than a forged body/header principal. Live-query authorization remains covered by emitter specs; the optional live-stream ownership probe is not added.

An in-memory uniqueness reservation may reject a contender even when its owner later rolls back; the JPA rollback proof does not promise that the in-memory store retries that contender. Authorization proofs record observed HTTP denial statuses without defining the transport's error-status or envelope policy.

These are native CI proofs, not evidence from .NET rendering alone. Local verification renders their sources and runs emitter/harness specs; Java/Maven execution and the actual concurrency outcomes must be checked in CI.
