// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_sequencing_all_writers_of_a_model : given.a_view_request
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    void should_serialize_single_and_multi_key_events_in_one_projection(bool root)
    {
        var context = Render(false);
        var code = Projector(context, "TotalProjection", root);
        code.ShouldContain("class IncreasedSequence");
        code.ShouldContain("class DecreasedSequence");
        AssertModelSequence(code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    void should_serialize_every_projector_when_one_writer_is_multi_key(bool root)
    {
        var context = Render(true);

        // Emit the single-key writer first: routing must not depend on emission order or projector package.
        AssertModelSequence(Projector(context, "OtherTotalProjection", root));
        AssertModelSequence(Projector(context, "TotalProjection", root));
    }

    static void AssertModelSequence(string code)
    {
        code.ShouldContain("return Optional.of(\"axonviews.views.counters.totals.Total\");");
        code.ShouldNotContain("return Optional.of(\"global\");");
        code.ShouldNotContain("return Optional.of(\"secondary\");");
    }

    static SemanticProjectionTransition Transition(SemanticId contract, string key) =>
        new(contract, new(AffectedInstanceCardinality.One, new SemanticValueExpression(new SemanticTextValue(key))), []);

    static string Projector(AxonEmissionContext context, string name, bool root)
    {
        if (root)
        {
            var owner = ScopeSelection.Locate(context.Request.Model.Application).Single(owner => owner.Slice.ReadModels.Any(model => model.Name == "Total"));
            var model = owner.Slice.ReadModels.Single(model => model.Name == "Total");
            var projection = owner.Slice.Projections.Single(projection => projection.Name == name);
            var fresh = new AxonEmissionContext(context.Request, new(context.Request.Profile));
            new RootProjectionEmitter(fresh, new(context.Request.Model.Application, "axonviews"), "axonviews.otherwriters." + JavaNames.Member(name), projection, model).Emit();
            context = fresh;
        }
        return Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith('/' + name + "Projector.java", StringComparison.Ordinal)));
    }

    AxonEmissionContext Render(bool split)
    {
        SemanticSlice Change(SemanticSlice slice)
        {
            var original = slice.Projections.FirstOrDefault(projection => projection.ReadModel == ReadModelId("Total"));
            if (original is null) return slice;
            var from = original.Scope!.From;
            var multi = original with { Scope = null, Transitions = [Transition(from[0].EventContract, "global"), Transition(from[0].EventContract, "secondary")] };
            var single = original with
            {
                Id = SemanticId.Create(SemanticKind.Projection, "OtherTotalProjection"),
                Name = "OtherTotalProjection",
                Scope = null,
                Transitions = [Transition(from[1].EventContract, "global")]
            };
            return slice with { Specifications = [], Projections = split ? [multi, single] : [multi with { Transitions = [.. multi.Transitions, .. single.Transitions] }] };
        }
        SemanticFeature Feature(SemanticFeature feature) => feature with { Features = [.. feature.Features.Select(Feature)], Slices = [.. feature.Slices.Select(Change)] };
        var application = _request.Model.Application;
        var model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application with { Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })] });
        var context = new AxonEmissionContext(_request with { Model = model }, new(_request.Profile));
        new AxonProjectionsEmitter().Emit(context);
        return context;
    }
}
