// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Conformance.for_SpecificationCoverage;

public class when_accounting_for_modeled_specifications : for_RenderCoverage.given.a_render_request
{
    SpecificationManifestEntry[] manifest;
    SpecificationResult[] results;
    HashSet<string> codes;
    string first;

    void Establish()
    {
        var identities = SemanticIdentities.Definitions(request.Model).Values.OfType<SemanticSpecification>().Select(specification => specification.Id.ToString()).Order(StringComparer.Ordinal).ToArray();
        first = identities[0];
        manifest = [.. identities.Select(identity => new SpecificationManifestEntry(identity, null))];
        results = [.. identities.Select(identity => new SpecificationResult(identity, true))];
        codes = new(StringComparer.OrdinalIgnoreCase) { "KNOWN" };
    }

    [Fact] void should_accept_one_passing_result_per_modeled_specification() => Check(manifest, results).IsClean.ShouldBeTrue();
    [Fact] void should_reject_missing_manifest_entries() => Check(manifest[1..], results).Issues.ShouldContain(issue => issue.Code == ConformanceCodes.MissingSpecification && issue.SemanticId == first);
    [Fact] void should_reject_duplicate_manifest_entries() => Check([.. manifest, manifest[0]], results).Issues.ShouldContain(issue => issue.Code == ConformanceCodes.DuplicateSpecification);
    [Fact] void should_reject_duplicate_results() => Check(manifest, [.. results, results[0]]).Issues.ShouldContain(issue => issue.Code == ConformanceCodes.DuplicateSpecification);
    [Fact] void should_reject_missing_enabled_results() => Check(manifest, results[1..]).Issues.ShouldContain(issue => issue.Code == ConformanceCodes.MissingSpecification);
    [Fact] void should_reject_failed_or_skipped_results() => Check(manifest, [results[0] with { Passed = false }, .. results[1..]]).Issues.ShouldContain(issue => issue.Code == ConformanceCodes.FailedSpecification);
    [Fact] void should_accept_registered_disabled_tests_without_results() => Check([manifest[0] with { DisabledCode = "KNOWN" }, .. manifest[1..]], results[1..]).IsClean.ShouldBeTrue();
    [Fact] void should_not_treat_a_disabled_skip_as_a_failed_enabled_test() => Check([manifest[0] with { DisabledCode = "KNOWN" }, .. manifest[1..]], [results[0] with { Passed = false }, .. results[1..]]).IsClean.ShouldBeTrue();
    [Fact] void should_compare_disabling_codes_ordinally() => Check([manifest[0] with { DisabledCode = "known" }, .. manifest[1..]], results).Issues.ShouldContain(issue => issue.Code == ConformanceCodes.UnregisteredCode);
    [Fact] void should_reject_unknown_manifest_identities() => Check([.. manifest, new("not-a-specification", "KNOWN")], results).Issues.ShouldContain(issue => issue.Code == ConformanceCodes.MalformedManifest);
    [Fact] void should_reject_unknown_result_identities() => Check(manifest, [.. results, new("not-a-specification", true)]).Issues.ShouldContain(issue => issue.Code == ConformanceCodes.MalformedManifest);

    CoverageReport Check(SpecificationManifestEntry[] entries, SpecificationResult[] executed) => SpecificationCoverage.Check(request.Model, entries, executed, codes);
}
