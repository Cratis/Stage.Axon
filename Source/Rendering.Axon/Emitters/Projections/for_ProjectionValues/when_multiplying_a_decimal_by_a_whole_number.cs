// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_ProjectionValues;

public class when_multiplying_a_decimal_by_a_whole_number : Specification
{
    string _projector;

    async Task Establish()
    {
        var root = Path.Combine(for_AxonArtifactRenderPlanner.given.blueprint_models.Root(), "Corpus/axon-samples/decimal-arithmetic");
        var compilation = await SemanticModelLoader.LoadAsync(root, ["decimal-arithmetic.play"], null, "DecimalArithmetic");
        Assert.True(compilation.Success, string.Join("; ", compilation.Diagnostics.Select(diagnostic => diagnostic.Code + ": " + diagnostic.Message)));
        var loaded = compilation.Loaded!;
        var request = new ArtifactRenderRequest(loaded.Model, loaded.Plan, AxonRendering.CreateProfile(), new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id))
        {
            ImplementationRequirements = loaded.ImplementationRequirements,
            ImplementationContents = loaded.ImplementationContents
        };
        var plan = new AxonArtifactRenderPlanner().Plan(request);
        _projector = Encoding.UTF8.GetString(plan.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/BasketTotalProjectionProjector.java", StringComparison.Ordinal)).Bytes.AsSpan());
    }

    [Fact] void should_convert_the_whole_number_factor_to_a_decimal() => _projector.ShouldContain("BigDecimal.valueOf(event.quantity().value())");
    [Fact] void should_wrap_the_converted_factor_in_the_decimal_concept() => _projector.ShouldContain("new Money(BigDecimal.valueOf(event.quantity().value()))");
    [Fact] void should_not_pass_the_raw_whole_number_to_the_decimal_concept() => _projector.ShouldNotContain("new Money(event.quantity().value())");
}
