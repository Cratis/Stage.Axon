// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Screenplay.Semantics.Execution;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence.for_PersistentViewTests;

public class when_rendering_a_reducer_only_model_with_a_live_query : Projections.for_AxonProjectionsEmitter.given.a_view_request
{
    ArtifactRenderPlan _plan;
    Exception? _exception;

    void Establish()
    {
        var id = ReadModelId("RebuiltTotal");
        SemanticSlice Slice(SemanticSlice slice)
        {
            var snapshot = slice.Queries.FirstOrDefault(query => query.ReadModel == id);
            return snapshot is null ? slice : slice with
            {
                Queries = [.. slice.Queries, snapshot with
                {
                    Id = SemanticId.Create(SemanticKind.Query, "Reduction.Counters.Rebuilt.LiveRebuiltTotals"),
                    Name = "LiveRebuiltTotals",
                    Delivery = SemanticQueryDelivery.Live
                }]
            };
        }
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
    }

    void Because() => _exception = Record.Exception(() => _plan = new AxonArtifactRenderPlanner().Plan(_request));

    [Fact] void should_not_throw_during_the_real_render() => Assert.Null(_exception);
    [Fact] void should_render_without_refusals() => _plan.Refusals.ShouldBeEmpty();
    [Fact] void should_emit_the_persistent_proof() => Has("RebuiltTotalPersistentTest").ShouldBeTrue();
    [Fact] void should_emit_the_multi_segment_proof() => Has("RebuiltTotalPersistentMultiSegmentTest").ShouldBeTrue();
    [Fact] void should_emit_the_store_proof() => Has("RebuiltTotalPersistentStoreTest").ShouldBeTrue();
    [Fact] void should_emit_the_legacy_proof() => Has("RebuiltTotalPersistentLegacyTest").ShouldBeTrue();
    [Fact] void should_emit_the_envelope_proof() => Has("RebuiltTotalPersistentEnvelopeTest").ShouldBeTrue();
    [Fact] void should_emit_the_fixture() => Has("RebuiltTotalPersistentFixture").ShouldBeTrue();
    [Fact] void should_use_the_persistent_profile() => Code("RebuiltTotalPersistentSupport").ShouldContain(".profiles(\"persistent\")");
    [Fact] void should_wire_the_placed_reducer() => Code("RebuiltTotalReducer").ShouldContain("return new RebuiltTotal(event.id(), (current == null ? 0L : current.amount()) + event.amount());");
    [Fact] void should_emit_the_snapshot_query() => Has("RebuiltTotalsHandler").ShouldBeTrue();
    [Fact] void should_emit_the_live_query() => Has("LiveRebuiltTotalsHandler").ShouldBeTrue();
    [Fact] void should_not_emit_the_reducer_default_dispatch_proof() => Has("RebuiltTotalPersistentDefaultDispatchTest").ShouldBeFalse();
    [Fact] void should_keep_the_projection_default_dispatch_proof() => Has("TotalPersistentDefaultDispatchTest").ShouldBeTrue();
    [Fact] void should_not_emit_the_reducer_subscription_proof() => Has("RebuiltTotalPersistentSubscriptionTest").ShouldBeFalse();
    [Fact] void should_keep_the_projection_subscription_proof() => Has("TotalPersistentSubscriptionTest").ShouldBeTrue();
    [Fact] void should_have_no_projection_for_the_reducer_model() => ScopeSelection.Select(_request).SelectMany(owner => owner.Slice.Projections).Any(projection => projection.ReadModel == ReadModelId("RebuiltTotal")).ShouldBeFalse();

    bool Has(string name) => _plan.Artifacts.Any(artifact => artifact.RelativePath.EndsWith('/' + name + ".java", StringComparison.Ordinal));
    string Code(string name) => Text(_plan.Artifacts.Single(artifact => artifact.RelativePath.EndsWith('/' + name + ".java", StringComparison.Ordinal)));
}
