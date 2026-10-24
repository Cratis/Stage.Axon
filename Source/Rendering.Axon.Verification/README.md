# Axon native verification harness

Run from the repository root after building the solution in Debug:

```bash
dotnet test Source/Rendering.Axon.Verification/Rendering.Axon.Verification.csproj --configuration Debug --no-build
dotnet run --project Source/Rendering.Axon.Verification/Rendering.Axon.Verification.csproj --configuration Debug --no-build -- render .ai-work/axon-corpus
```

The destination must not exist. The harness renders 15 live blueprints, three verification samples, four individual view models, and their combined application. Each compilation and render must succeed without refusals. The output includes a Maven reactor and an application list.

Run the generated Maven wrapper with Java 21, then check the actual Surefire reports:

```bash
(cd .ai-work/axon-corpus && sh ./mvnw -B -fae verify)
dotnet run --project Source/Rendering.Axon.Verification/Rendering.Axon.Verification.csproj --configuration Debug --no-build -- verify .ai-work/axon-corpus
node Source/Rendering.Axon.Verification/boot-smoke.mjs .ai-work/axon-corpus .ai-work/axon-boot-logs
```

Every application must execute passing native tests. Missing, failed, empty, or entirely skipped reports fail verification. Disabled tests are counted by reason; unknown reasons or counts above `disabled-tests.json` fail. Its initial budget preserves the measured pre-view corpus baseline of 126 `STAGE-AXON-SPEC` cases, not an unmeasured post-integration baseline. Any additional reason or budget needs explicit review; do not raise it merely to obtain a passing run.

The boot smoke starts each packaged application, checks its intentional root HTTP 404, and stops every process it owns. It does not assert modeled command or query behavior; those assertions belong to the generated native tests. CI should put generated output and logs under `runner.temp`, not in the checkout.
