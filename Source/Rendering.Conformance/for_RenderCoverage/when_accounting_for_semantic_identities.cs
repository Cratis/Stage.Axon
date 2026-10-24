// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Conformance.for_RenderCoverage;

public class when_accounting_for_semantic_identities : given.a_render_request
{
    CoverageReport complete;
    CoverageReport warnings;
    CoverageReport missing;
    CoverageReport unknown;

    void Because()
    {
        complete = RenderCoverage.Check(request, Plan([PlannedArtifact.CreateText("all.txt", "realized", [.. Identities()])], []), new HashSet<string>());
        var diagnostics = Identities().Select(identity => new ArtifactRenderDiagnostic("KNOWN", ArtifactRenderDiagnosticSeverity.Warning, "Closest supported equivalent.", identity)).ToArray();
        warnings = RenderCoverage.Check(request, Plan([], [.. diagnostics]), new HashSet<string> { "KNOWN" });
        missing = RenderCoverage.Check(request, Plan([], []), new HashSet<string>());
        unknown = RenderCoverage.Check(request, Plan([], [new("known", ArtifactRenderDiagnosticSeverity.Warning, "Not registered with this casing.", request.Scope.Artifact)]), new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "KNOWN" });
    }

    [Fact] void should_account_for_realized_semantic_sources() => complete.IsClean.ShouldBeTrue();
    [Fact] void should_allow_explicit_weaker_equivalent_warnings() => warnings.IsClean.ShouldBeTrue();
    [Fact] void should_find_unaccounted_identities() => missing.Issues.ShouldContain(issue => issue.Code == ConformanceCodes.UnaccountedIdentity);
    [Fact] void should_identify_the_unaccounted_subject() => missing.Issues.ShouldContain(issue => issue.SemanticId == request.Scope.Artifact.ToString());
    [Fact] void should_match_registered_codes_ordinally() => unknown.Issues.ShouldContain(issue => issue.Code == ConformanceCodes.UnregisteredCode);
    [Fact] void should_keep_diagnostic_context() => unknown.Issues.Single(issue => issue.Code == ConformanceCodes.UnregisteredCode).Message.ShouldContain("known");
    [Fact] void should_keep_conflict_code_stable() => ConformanceCodes.RealizedAndRefused.ShouldEqual("STAGE-CONF-002");
    [Fact] void should_allow_issues_without_a_semantic_subject() => new ConformanceIssue("test", "message").SemanticId.ShouldBeNull();
}
