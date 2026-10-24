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

Every application must execute passing native tests. Missing, failed, empty, or entirely skipped reports fail verification. `disabled-tests.json` records an exact count and explanation per reason, measured independently from all 23 generated `stage-specifications.txt` files and Java `@Disabled` annotations: 63 `STAGE-AXON-0501` view gaps, two `STAGE-AXON-0306` reducer gaps and three `STAGE-AXON-SPEC` custom-package view gaps. The total is 68; this is source accounting, not a native execution result. Unknown reasons, increases, decreases, absent reasons and unexplained or zero-count entries fail. Ratchet the allowlist whenever lowering improves; do not raise it merely to obtain a passing run.

The opt-in `PersistentRestartProbe` is outside default Surefire discovery, so it adds no conditional skip to the default corpus. After the normal native checks, explicitly run it against a view application:

```bash
(cd .ai-work/axon-corpus && sh ./mvnw -B -pl orders -Dtest=PersistentRestartProbe -Dstage.restart=true -Dstage.expectViews=true test)
```

This starts the application twice under `persistent`, requires generated in-memory view stores on both boots, and checks event/claim survival in a shared H2 file. The view rows remain volatile, as documented by `STAGE-AXON-0308`. The opt-in probe is a separate proof, not a replacement for the default corpus checks.

The boot smoke starts each packaged application, checks its intentional root HTTP 404, and stops every process it owns. It does not assert modeled command or query behavior; those assertions belong to the generated native tests. CI should put generated output and logs under `runner.temp`, not in the checkout.
