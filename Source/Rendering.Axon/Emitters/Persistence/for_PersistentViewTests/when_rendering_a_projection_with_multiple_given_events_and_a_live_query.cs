// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Screenplay.Semantics.Execution;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence.for_PersistentViewTests;

public class when_rendering_a_projection_with_multiple_given_events_and_a_live_query : Projections.for_AxonProjectionsEmitter.given.a_view_request
{
    ArtifactRenderPlan _plan;
    int _givenEvents;

    void Establish()
    {
        var id = ReadModelId("Total");
        SemanticSlice Slice(SemanticSlice slice) => slice with
        {
            Specifications = [.. slice.Specifications.Where(spec => !spec.ThenReadModels.Any(expected => expected.ReadModel == id) || spec.GivenEvents.Length == 2)]
        };
        SemanticFeature Feature(SemanticFeature feature) => feature with
        {
            Features = [.. feature.Features.Select(Feature)],
            Slices = [.. feature.Slices.Select(Slice)]
        };
        var application = _request.Model.Application;
        var model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application with
        {
            Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })]
        });
        _request = _request with { Model = model, ExecutionPlan = SemanticExecutionPlan.CompileForSpecifications(model).Plan };
        _givenEvents = ScopeSelection.Select(_request).SelectMany(owner => owner.Slice.Specifications)
            .Single(spec => spec.ThenReadModels.Any(expected => expected.ReadModel == id)).GivenEvents.Length;
    }

    void Because() => _plan = new AxonArtifactRenderPlanner().Plan(_request);

    [Fact] void should_have_two_given_events_in_the_matching_specification() => _givenEvents.ShouldEqual(2);
    [Fact] void should_render_without_refusals() => _plan.Refusals.ShouldBeEmpty();
    [Fact] void should_emit_the_live_query() => Has("LiveTotalsHandler").ShouldBeTrue();
    [Fact] void should_not_emit_the_single_event_subscription_proof() => Has("TotalPersistentSubscriptionTest").ShouldBeFalse();
    [Fact] void should_keep_the_persistent_proof() => Has("TotalPersistentTest").ShouldBeTrue();
    [Fact] void should_keep_the_multi_segment_proof() => Has("TotalPersistentMultiSegmentTest").ShouldBeTrue();
    [Fact] void should_keep_the_store_proof() => Has("TotalPersistentStoreTest").ShouldBeTrue();
    [Fact] void should_keep_the_legacy_proof() => Has("TotalPersistentLegacyTest").ShouldBeTrue();
    [Fact] void should_keep_the_envelope_proof() => Has("TotalPersistentEnvelopeTest").ShouldBeTrue();
    [Fact] void should_keep_the_default_dispatch_proof() => Has("TotalPersistentDefaultDispatchTest").ShouldBeTrue();
    [Fact] void should_keep_the_fixture() => Has("TotalPersistentFixture").ShouldBeTrue();
    [Fact] void should_keep_the_support() => Has("TotalPersistentSupport").ShouldBeTrue();

    bool Has(string name) => _plan.Artifacts.Any(artifact => artifact.RelativePath.EndsWith('/' + name + ".java", StringComparison.Ordinal));
}
