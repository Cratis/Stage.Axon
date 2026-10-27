// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Java;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_PublicApi;

public class when_listing_exported_types : Specification
{
    Type[] exported;

    void Because() => exported = [.. typeof(AxonRendering).Assembly.GetExportedTypes().Where(type => !IsSpecification(type))];

    [Fact]
    void should_expose_only_the_reviewed_public_types() => exported.ToHashSet().SetEquals(
    [
        typeof(AxonRendering),
        typeof(AxonArtifactRenderPlanner),
        typeof(AxonRenderRequest),
        typeof(AxonRenderOptions),
        typeof(SimpleNameAlreadyClaimed)
    ]).ShouldBeTrue();

    static bool IsSpecification(Type type) => typeof(Specification).IsAssignableFrom(type) ||
        (type.Namespace?.Split('.').Any(segment => segment.StartsWith("for_", StringComparison.Ordinal) || segment.StartsWith("when_", StringComparison.Ordinal) || segment == "given") ?? false);
}
