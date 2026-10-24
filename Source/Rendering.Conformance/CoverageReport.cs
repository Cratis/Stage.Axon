// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace Cratis.Stage.Rendering.Conformance;

/// <summary>Contains all violations found by one conformance check.</summary>
/// <param name="Issues">The violations, not a record of passing checks.</param>
public record CoverageReport(ImmutableArray<ConformanceIssue> Issues)
{
    /// <summary>Gets whether the check found no violations.</summary>
    public bool IsClean => Issues.IsEmpty;
}
