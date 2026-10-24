// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Contracts.Rendering;

namespace Cratis.Stage.Rendering.Conformance;

/// <summary>Checks that a renderer accounts for every reachable semantic identity.</summary>
public static class RenderCoverage
{
    /// <summary>Checks scoped identities, conflicting realizations and registered diagnostics.</summary>
    /// <param name="request">The selected model and scopes.</param>
    /// <param name="plan">The resulting artifact plan.</param>
    /// <param name="registeredCodes">Target-owned codes; comparison is ordinal and case-sensitive regardless of this set's comparer.</param>
    /// <returns>All coverage violations. Warnings account for unsupported constructs.</returns>
    public static CoverageReport Check(ArtifactRenderRequest request, ArtifactRenderPlan plan, IReadOnlySet<string> registeredCodes)
    {
        var issues = new List<ConformanceIssue>();
        var codes = registeredCodes.ToHashSet(StringComparer.Ordinal);
        var realized = plan.Artifacts.SelectMany(artifact => artifact.Sources).ToHashSet();
        var refused = plan.Refusals.Select(refusal => refusal.Artifact).ToHashSet();
        var accounted = realized.Concat(refused).Concat(plan.Diagnostics.Select(diagnostic => diagnostic.Artifact)).ToHashSet();
        foreach (var identity in SemanticIdentities.Reachable(request).OrderBy(identity => identity.ToString(), StringComparer.Ordinal))
        {
            if (!accounted.Contains(identity)) issues.Add(new(ConformanceCodes.UnaccountedIdentity, "The reachable semantic identity has no artifact, diagnostic or refusal.", identity.ToString()));
            if (realized.Contains(identity) && refused.Contains(identity)) issues.Add(new(ConformanceCodes.RealizedAndRefused, "The identity is both realized and refused.", identity.ToString()));
        }
        foreach (var diagnostic in plan.Diagnostics.Where(diagnostic => !codes.Contains(diagnostic.Code)))
            issues.Add(new(ConformanceCodes.UnregisteredCode, "Unregistered diagnostic code: " + diagnostic.Code, diagnostic.Artifact.ToString()));
        issues.AddRange(RefusalIssues(plan, codes));
        return new([.. issues]);
    }

    static IEnumerable<ConformanceIssue> RefusalIssues(ArtifactRenderPlan plan, HashSet<string> codes) =>
        plan.Refusals.SelectMany(refusal => refusal.Codes.Where(code => !codes.Contains(code))
            .Select(code => new ConformanceIssue(ConformanceCodes.UnregisteredCode, "Unregistered refusal code: " + code, refusal.Artifact.ToString())));
}
