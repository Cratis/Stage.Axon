# Stage.Axon

The Axon Framework JVM render target for Cratis Stage. It plans Java applications from Screenplay executable semantic models using the published `Cratis.Stage.Contracts` 4.197.0 package. The planner, embedded Java templates, and native verification harness retain their history from Cratis/Stage. This repository does not contain the Stage engine.

`Source/Rendering.Conformance` is a vendored, Contracts-only conformance helper until it is published separately. The verification corpus retains 15 live blueprints, three samples, and four individual view models (also rendered together): 23 applications. Their models remain the behavioral input contract.

The `sample-custom-packages` application composes four retained local documents with six documents from `Cratis.Screenplay.CanonicalCorpus` 4.241.0: the Tracking feature and its five slices (`add-comment-slice`, `close-work-item-slice`, `rename-work-item-slice`, `work-item-comments-slice`, `work-item-details-slice`). Each is selected from `ScreenCompositionCorpus.V1.SourceForms.Single(form => form.Name == "folder")` by its public stable key, and its exact package bytes are written into a disposable input directory; a missing, duplicate, path-mismatched or empty document, or a document that would shadow a local copy, fails without a fallback. Every other corpus input and all 23 application names remain unchanged. The package is a verification dependency only, with no production renderer dependency. [`Corpus/README.md`](Corpus/README.md) records the package-sourced documents and why every other vendored model is retained.

## Build and specs

Requires .NET 10. Run from this directory:

```sh
dotnet build Stage.Axon.slnx -c Release -warnaserror
dotnet build Stage.Axon.slnx -c Debug
dotnet test Stage.Axon.slnx -c Debug --no-build
```

Specs are inline in all three projects and are compiled only in Debug. The extracted baseline is 389 planner specs, 16 verification specs, and 33 conformance specs (438 total, no skips). Repository analyzers run during both builds; Release treats warnings as errors.

## Native proof

Use JDK 21 and the generated Maven wrapper. The render destination must not exist.

```sh
dotnet run --project Source/Rendering.Axon.Verification -c Debug --no-build -- render .ai-work/corpus
(cd .ai-work/corpus && sh ./mvnw -B -fae verify)
dotnet run --project Source/Rendering.Axon.Verification -c Debug --no-build -- verify .ai-work/corpus
```

The last command requires passing native tests in every application and validates the exact disabled-test budget: `STAGE-AXON-0501=5`, `STAGE-AXON-0306=2`, `STAGE-AXON-SPEC=3`. Missing, failing, empty, all-skipped, unknown-reason, exceeded, and stale budgets fail. Do not raise budgets to obtain a passing run. See [the harness guide](Source/Rendering.Axon.Verification/README.md) for optional restart and boot smoke checks and [the target guide](Documentation/guides/third-party-render-target.md) for renderer contracts and Axon limitations.

The build workflow is an extraction skeleton, not publication automation. It runs Release/Debug builds, all three spec projects with nonempty-result guards, and the 23-app native proof. It needs no containers or Stage checkout. No package publication is configured.

Licensed under the [MIT license](LICENSE).
