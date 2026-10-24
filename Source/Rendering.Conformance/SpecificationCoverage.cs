// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;

namespace Cratis.Stage.Rendering.Conformance;

/// <summary>Accounts for modeled tests without confusing disabled with passed.</summary>
public static class SpecificationCoverage
{
    /// <summary>Requires one manifest entry and either a registered disabling code or one passing native result per modeled specification.</summary>
    /// <param name="model">The model whose specifications must be accounted for.</param>
    /// <param name="manifest">The renderer's explicit specification manifest.</param>
    /// <param name="results">Correlated native test results; skipped tests must have Passed=false.</param>
    /// <param name="registeredCodes">Target-owned codes; comparison is ordinal and case-sensitive regardless of this set's comparer.</param>
    /// <returns>Missing, duplicated, failed or malformed coverage entries.</returns>
    public static CoverageReport Check(ExecutableSemanticModel model, IEnumerable<SpecificationManifestEntry> manifest, IEnumerable<SpecificationResult> results, IReadOnlySet<string> registeredCodes)
    {
        var expected = SemanticIdentities.Definitions(model).Values.OfType<SemanticSpecification>().Select(specification => specification.Id.ToString()).ToHashSet(StringComparer.Ordinal);
        var entries = manifest.ToLookup(entry => entry.SemanticId, StringComparer.Ordinal);
        var executed = results.ToLookup(result => result.SemanticId, StringComparer.Ordinal);
        var codes = registeredCodes.ToHashSet(StringComparer.Ordinal);
        var issues = new List<ConformanceIssue>();
        foreach (var unknown in entries.Select(group => group.Key).Concat(executed.Select(group => group.Key)).Distinct(StringComparer.Ordinal).Except(expected, StringComparer.Ordinal))
            issues.Add(new(ConformanceCodes.MalformedManifest, "The manifest or result names an unknown specification.", unknown));
        var check = new SpecificationCoverageCheck(codes, issues);
        foreach (var identity in expected.Order(StringComparer.Ordinal)) check.Check(identity, [.. entries[identity]], [.. executed[identity]]);
        return new([.. issues]);
    }
}
