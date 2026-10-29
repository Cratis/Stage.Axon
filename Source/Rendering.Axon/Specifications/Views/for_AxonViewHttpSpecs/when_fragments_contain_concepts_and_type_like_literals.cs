// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views.for_AxonViewHttpSpecs;

public class when_fragments_contain_concepts_and_type_like_literals : for_AxonViewSpecsEmitter.given.a_populated_concept_collision
{
    const string Literal = "java.util.List org.junit.jupiter.api.Assertions example.events.Increased";
    SemanticId eventStatus;
    SemanticQueryDelivery delivery;
    string code;

    void Establish()
    {
        Prepare(false, "List");
        var concept = _request.Model.Application.Concepts.Single(candidate => candidate.Name == "List").Id;
        eventStatus = ScopeSelection.Select(_request).SelectMany(owner => owner.Slice.Events).Single(contract => contract.Id == _spec.GivenEvents.Single().EventContract).Properties.Single(property => property.Type.Target == concept).Id;
        delivery = _located.Slice.Queries.Single(query => query.Name == "GetCategory").Delivery;
        var status = _located.Slice.ReadModels.Single().Properties.Single(property => property.Type.Target == concept);
        _spec = _spec with { ThenReadModels = [.. _spec.ThenReadModels.Select(expected => expected with { Values = [.. expected.Values, new(status.Id, new SemanticTextValue(Literal))] })] };
        var application = _request.Model.Application with { Modules = [.. _request.Model.Application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })] };
        _request = _request with { Model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application) };
        _context = new(_request, new(_request.Profile));
        _located = ScopeSelection.Select(_request).Single(owner => owner.Slice.Id == _located.Slice.Id);
        _spec = _located.Slice.Specifications.Single();
    }

    void Because()
    {
        var model = _located.Slice.ReadModels.Single();
        AxonSpecificationsEmitter.Register(_context, _spec.Id, "example.ViewTest", "listingTheRegisteredCategory");
        new AxonViewHttpSpecs(_context, new ViewJavaTypes(_request.Model.Application, Java.JavaNames.PackageSegment(_request.Model.Application.Name)), _located, model).Register(_located.Slice.Projections.Single(), _spec);
        AxonHttpTests.Emit(_context);
        code = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/ModeledHttpRoundTripTest.java", StringComparison.Ordinal)));
    }

    SemanticFeature Feature(SemanticFeature feature) => feature with
    {
        Features = [.. feature.Features.Select(Feature)],
        Slices = [.. feature.Slices.Select(Slice)]
    };

    SemanticSlice Slice(SemanticSlice slice) => slice.Id == _located.Slice.Id ? slice with
    {
        Queries = [.. slice.Queries.Select(query => query with { Delivery = delivery, Authorization = null })],
        Specifications = [Specification()]
    } : slice;

    SemanticSpecification Specification() => _spec with
    {
        GivenEvents = [.. _spec.GivenEvents.Select(given => given with
        {
            Values = [.. given.Values.Select(value => value.TargetProperty == eventStatus ? value with { Value = new SemanticTextValue(Literal) } : value)]
        })]
    };

    [Fact] void should_import_the_concept_used_by_the_event_and_predicate() => code.ShouldContain("import nativeviews.concepts.List;");
    [Fact] void should_shorten_the_concept_in_the_given_event() => code.Split('\n').Single(line => line.Contains("projector.on(", StringComparison.Ordinal)).ShouldContain("new List(\"" + Literal + "\")");
    [Fact] void should_shorten_the_concept_in_the_expected_predicate() => code.ShouldContain("Objects.equals(row.status(), new List(\"" + Literal + "\"))");
    [Fact] void should_keep_the_colliding_collection_qualified() => code.ShouldContain("new TypeReference<java.util.List<Categories>>() {}");
    [Fact] void should_not_import_the_colliding_collection() => code.ShouldNotContain("import java.util.List;");
    [Fact] void should_resolve_the_repository_of_the_model() => code.ShouldContain("application.getBean(CategoriesRepository.class)");
    [Fact] void should_not_import_types_mentioned_only_by_a_literal() => code.ShouldNotContain("import example.events.Increased;");
}
