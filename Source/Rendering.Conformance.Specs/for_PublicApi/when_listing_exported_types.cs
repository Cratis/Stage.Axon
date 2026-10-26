// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Conformance.for_PublicApi;

public class when_listing_exported_types : Specification
{
    Type[] exported;

    void Because() => exported = [.. typeof(RenderCoverage).Assembly.GetExportedTypes().Where(type => !IsSpecification(type))];

    [Fact]
    void should_expose_only_the_reviewed_public_types() => exported.ToHashSet().SetEquals(
    [
        typeof(ConformanceCodes),
        typeof(ConformanceIssue),
        typeof(CoverageReport),
        typeof(JUnitXmlResults),
        typeof(RenderCoverage),
        typeof(RenderDeterminism),
        typeof(SpecificationCoverage),
        typeof(SpecificationManifestEntry),
        typeof(SpecificationResult),
        typeof(StageSpecificationManifest)
    ]).ShouldBeTrue();

    static bool IsSpecification(Type type) => typeof(Specification).IsAssignableFrom(type) ||
        (type.Namespace?.Split('.').Any(segment => segment.StartsWith("for_", StringComparison.Ordinal) || segment.StartsWith("when_", StringComparison.Ordinal) || segment == "given") ?? false);
}
