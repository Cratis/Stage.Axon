// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_ViewJavaTypes;

public class when_wrapping_enumeration_values : Specification
{
    ViewJavaTypes types;
    ViewJavaTypes numeric;
    SemanticTypeReference reference;

    void Establish()
    {
        var loaded = for_AxonArtifactRenderPlanner.given.blueprint_models.Load("approvals").GetAwaiter().GetResult();
        var model = loaded.Model.Application;
        var concept = model.Concepts.Single(candidate => candidate.Name == "RequestState");
        reference = ScopeSelection.Locate(model).SelectMany(owner => owner.Slice.ReadModels).SelectMany(readModel => readModel.Properties).First(property => property.Type.Target == concept.Id).Type;
        types = new(model);
        numeric = new(model with { Concepts = [.. model.Concepts.Select(candidate => candidate.Id == concept.Id ? candidate with { Numbers = [10, 20, 30, 40, 50] } : candidate)] });
    }

    [Fact] void should_use_the_string_enum_factory() => types.Wrap(reference, "\"submitted\"").ShouldEqual("sample.concepts.RequestState.fromValue(\"submitted\")");
    [Fact] void should_not_construct_enum_instances_in_projection_literals() => types.Parse(reference, "\"submitted\"").ShouldContain("RequestState.fromValue(");
    [Fact] void should_use_the_integer_factory_for_numbered_enums() => numeric.Wrap(reference, "20L").ShouldEqual("sample.concepts.RequestState.fromValue(Math.toIntExact(20L))");
    [Fact] void should_parse_numbered_enum_query_arguments_as_numbers() => numeric.Parse(reference, "state").ShouldContain("Math.toIntExact(Long.valueOf(state))");
    [Fact] void should_not_invent_an_invalid_enum_zero_as_initial_projection_state() => numeric.Default(reference).ShouldEqual("null");
}
