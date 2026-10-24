// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Conformance;

internal sealed class SpecificationCoverageCheck(HashSet<string> codes, List<ConformanceIssue> issues)
{
    internal void Check(string identity, SpecificationManifestEntry[] entries, SpecificationResult[] results)
    {
        if (entries.Length == 0) issues.Add(new(ConformanceCodes.MissingSpecification, "The modeled specification is missing from the manifest.", identity));
        if (entries.Length > 1 || results.Length > 1) issues.Add(new(ConformanceCodes.DuplicateSpecification, "The specification has duplicate manifest entries or results.", identity));
        CheckDisabled(identity, entries);
        if (entries.Length != 1 || entries[0].DisabledCode is not null) return;
        CheckResults(identity, results);
    }

    void CheckDisabled(string identity, SpecificationManifestEntry[] entries)
    {
        foreach (var disabled in entries.Where(entry => entry.DisabledCode is not null))
        {
            if (!codes.Contains(disabled.DisabledCode!)) issues.Add(new(ConformanceCodes.UnregisteredCode, "Unregistered disabling code: " + disabled.DisabledCode, identity));
        }
    }

    void CheckResults(string identity, SpecificationResult[] results)
    {
        if (results.Length == 0) issues.Add(new(ConformanceCodes.MissingSpecification, "The enabled specification has no native result.", identity));
        foreach (var failed in results.Where(result => !result.Passed)) issues.Add(new(ConformanceCodes.FailedSpecification, "The enabled specification failed, errored or was skipped.", failed.SemanticId));
    }
}
