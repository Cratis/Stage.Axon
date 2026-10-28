// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_emitting_a_decimal_column : given.a_view_request
{
    string _entity;

    async Task Because()
    {
        var model = await SemanticModelLoader.LoadFromPathAsync(Path.Combine(Root(), "Corpus", "axon-samples", "decimal-arithmetic"), null, "Pricing");
        var request = new ArtifactRenderRequest(model.Model, model.Plan, AxonRendering.CreateProfile(), new(ArtifactRenderScopeKind.Application, model.Model.Application.Id))
        {
            ImplementationRequirements = model.ImplementationRequirements,
            ImplementationContents = model.ImplementationContents
        };
        var context = new AxonEmissionContext(request, new(request.Profile));
        new AxonProjectionsEmitter().Emit(context);
        _entity = Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/BasketTotalEntity.java", StringComparison.Ordinal)));
    }

    [Fact] void should_store_the_decimal_without_a_fixed_scale() => _entity.ShouldContain("@Column(name = \"p_total\", columnDefinition = \"DECFLOAT\")");
    [Fact] void should_not_round_to_a_fixed_precision() => _entity.ShouldNotContain("scale = ");
    [Fact] void should_not_limit_the_key_length_to_the_jpa_default() => _entity.ShouldContain("@Column(name = \"row_key\", columnDefinition = \"VARCHAR(1000000000)\")");
    [Fact] void should_not_use_a_qualified_annotation() => _entity.ShouldNotContain("@jakarta.");
}
