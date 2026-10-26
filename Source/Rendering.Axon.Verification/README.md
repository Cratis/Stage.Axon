# Axon native verification harness

Run from the repository root after building the solution in Debug:

```bash
dotnet test Source/Rendering.Axon.Verification/Rendering.Axon.Verification.csproj --configuration Debug --no-build
dotnet run --project Source/Rendering.Axon.Verification/Rendering.Axon.Verification.csproj --configuration Debug --no-build -- render .ai-work/axon-corpus
```

The destination must not exist. The harness renders 15 live blueprints, three verification samples, four individual view models, and their combined application. Each compilation and render must succeed without refusals. The output includes a Maven reactor and an application list.

The custom-package sample uses mixed packaged and retained input. `Cratis.Screenplay.CanonicalCorpus` is pinned to 4.241.0; `ScreenCompositionCorpus.V1.SourceForms.Single(form => form.Name == "folder").Documents.Single(document => document.StableKey == "workspaces-tracking-feature")` supplies the exact bytes at display path `Workspaces/Tracking/Tracking.play`. The harness copies the nine retained custom-package documents into an owned disposable directory under `.ai-work/axon5/`, adds the package document, and keeps that scope alive through compilation and rendering. Disposal removes the composed input tree, including on failure. Missing, duplicate, path-mismatched, or empty selection fails before output creation; there is no vendored fallback.

The covered local Tracking document is removed. All other inputs and all 23 application names are retained without importing the canonical Workspaces application or identity catalog. This is a one-document adoption, not a coverage claim for the remaining inputs. The same input helper and package are linked only into Debug renderer specs; the production renderer does not depend on CanonicalCorpus.

Run the generated Maven wrapper with Java 21, then check the actual Surefire reports:

```bash
(cd .ai-work/axon-corpus && sh ./mvnw -B -fae verify)
dotnet run --project Source/Rendering.Axon.Verification/Rendering.Axon.Verification.csproj --configuration Debug --no-build -- verify .ai-work/axon-corpus
node Source/Rendering.Axon.Verification/boot-smoke.mjs .ai-work/axon-corpus .ai-work/axon-boot-logs
```

Every application must execute passing native tests. Missing, failed, empty, or entirely skipped reports fail verification. `disabled-tests.json` records an exact count and explanation per reason, measured independently from all 23 generated `stage-specifications.txt` files and Java `@Disabled` annotations: two `STAGE-AXON-0306` reducer gaps. The total is 2; this is source accounting, not a native execution result. Unknown reasons, increases, decreases, absent reasons and unexplained or zero-count entries fail. Ratchet the allowlist whenever lowering improves; do not raise it merely to obtain a passing run.

The opt-in `PersistentRestartProbe` is outside default Surefire discovery, so it adds no conditional skip to the default corpus. After the normal native checks, explicitly run it against a view application:

```bash
(cd .ai-work/axon-corpus && sh ./mvnw -B -pl orders -Dtest=PersistentRestartProbe -Dstage.restart=true -Dstage.expectViews=true test)
```

Two scenarios execute per selected module: a scan-free, test-only viewless fixture preserves events and uniqueness claims across two persistent boots; the actual generated view application refuses two persistent starts with `STAGE-AXON-0308`, before factory/resource/lifecycle activity, HTTP initialization or readiness. Preseeded nonempty event, claim and synthetic token rows are compared in full by SELECT-only logical snapshots after each refusal; a fresh-file attempt must also refuse without creating database files. The fixture does not remove or replace any production view. Without `stage.expectViews=true`, a successful main-application probe must explicitly be viewless.

Rows and projector indexes are volatile, while durable checkpoints may skip prior events after restart. Automatic replay is not established recovery; persistent startup with generated views is refused. The full persistence/recovery gap remains open. Default-profile native tests, including ordinary lifecycle guard tests and deliberate Given-event projection replay, do not establish startup recovery. The opt-in probe is separate safety evidence, not a replacement for the default corpus checks.

The boot smoke starts each packaged application, checks its intentional root HTTP 404, and stops every process it owns. It does not assert modeled command or query behavior; those assertions belong to the generated native tests. CI should put generated output and logs under `runner.temp`, not in the checkout.
