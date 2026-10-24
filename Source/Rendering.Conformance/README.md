# Renderer conformance (preview)

`Cratis.Stage.Rendering.Conformance` checks whether an artifact renderer accounts for its Screenplay input, produces deterministic output, and accounts for every modeled specification. This target-neutral preview API may change; it is not a certification or a claim that disabled behavior works.

## Check a renderer

- Call `RenderCoverage.Check(request, plan, registeredCodes)` after planning. The selected scope and additional scopes include their nested definitions and referenced semantic identities. Every reachable identity needs an artifact source, diagnostic or refusal. An explicit weaker-equivalent warning counts as accounting for a construct; it does not prove equivalent behavior.
- Call `RenderDeterminism.Check(planner, request)` with a fixed clock and generation seed wherever the renderer supports those inputs. The check plans twice and compares paths, kinds, actual bytes, semantic sources, diagnostics, refusals, implementation gaps and required removals. Time- or randomness-driven differences are violations, not ignored noise.
- Read `stage-specifications.txt` with `StageSpecificationManifest.Read(path)`. Call `JUnitXmlResults.ReadDirectory(directory)` on native JUnit XML, then `SpecificationCoverage.Check(model, manifest, results, registeredCodes)`. Every modeled specification needs exactly one manifest entry and either one passing result or an explicit registered disabling code. Missing, failed, errored, skipped enabled tests and duplicates are violations. Disabled tests are accounted for, never reported as passing.

All checks return `CoverageReport`; inspect `Issues` or `IsClean`. Issue identities are opaque strings and may be absent for plan-level problems. Registered codes are always matched ordinally and case-sensitively, regardless of the supplied set's comparer. Coverage does not run a build or native tests for you.

## Native result bridge

Manifest rows contain two tab-separated columns: `SemanticId` and `fully.qualified.TestClass#method`. Disabled entries insert a middle column, `disabled CODE`. Duplicate rows are preserved so coverage can diagnose them. Malformed rows throw `FormatException` containing `STAGE-CONF-008`.

The XML reader accepts `testsuite` and `testsuites` roots, semantic testcase display names, and semantic `display-name` testcase properties. For ordinary native method names it searches the report directory and its ancestors for the nearest `stage-specifications.txt` and correlates the exact class and method. Uncorrelated helper tests are ignored; they cannot satisfy modeled specification coverage. Results preserve duplicates and map failure, error and skip nodes to `Passed = false`.

DTDs and external entities are prohibited. XML is limited to 32 MiB per file. Malformed or unsafe XML throws `FormatException` with `STAGE-CONF-008`; unreadable files propagate their I/O error.

## Stable codes

| Code | Violation |
| --- | --- |
| `STAGE-CONF-001` | Unaccounted reachable identity |
| `STAGE-CONF-002` | Realized and refused identity (also prohibited by the plan contract) |
| `STAGE-CONF-003` | Unregistered diagnostic, refusal or disabling code |
| `STAGE-CONF-004` | Nondeterministic artifacts or typed metadata |
| `STAGE-CONF-005` | Missing manifest entry or enabled result |
| `STAGE-CONF-006` | Duplicate manifest entry or result |
| `STAGE-CONF-007` | Failed, errored or skipped enabled specification |
| `STAGE-CONF-008` | Malformed manifest/result or unknown specification identity |
