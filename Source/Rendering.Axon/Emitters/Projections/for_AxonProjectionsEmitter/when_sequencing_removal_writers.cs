// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_sequencing_removal_writers : Persistence.for_PersistentViewAdmission.given.a_flat_view
{
    [Fact]
    void should_use_the_store_map_for_both_create_and_remove()
    {
        var code = Code(Render(slice => slice), "MetricProjectionProjector");
        var key = code.Split('\n').Single(line => line.Contains("store.update(Map.ofEntries(", StringComparison.Ordinal)).Trim()["store.update(".Length..].Split(", current ->", StringSplitOptions.None)[0];
        code.ShouldContain($"store.remove({key});");
        code.Split($"return Optional.of({key});", StringSplitOptions.None).Length.ShouldEqual(3);
        code.ShouldNotContain("return Optional.of(\"axonviews.metrics.monthly.metrics.Metric\");");
        foreach (var policy in new[] { "MetricCountedSequence", "MetricRemovedSequence" })
        {
            code.ShouldContain($"@SequencingPolicy(type = {policy}.class)");
            code.Split($"public static final class {policy} ", StringSplitOptions.None)[1].Split("public static final class ", StringSplitOptions.None)[0].ShouldContain($"return Optional.of({key});");
        }
    }

    [Fact]
    void should_allocate_a_collision_safe_policy_for_a_removal_only_contract()
    {
        var context = Render(slice => slice with
        {
            ReadModels = [.. slice.ReadModels.Select(model => model.Name == "Metric" ? model with { Name = "MetricRemovedSequence" } : model)]
        });
        var projection = ScopeSelection.Locate(context.Request.Model.Application).SelectMany(owner => owner.Slice.Projections).Single(projection => projection.Name == "MetricProjection");
        var removal = projection.Scope!.Removals.Single();
        ProjectionTransitions.From(projection).Any(from => from.EventContract == removal.EventContract).ShouldBeFalse();
        var code = Code(context, "MetricProjectionProjector");
        code.ShouldContain("@SequencingPolicy(type = MetricRemovedSequence2.class)");
        code.ShouldContain("public static final class MetricRemovedSequence2 implements org.axonframework.messaging.core.sequencing.SequencingPolicy<EventMessage>");
        code.ShouldContain("store.remove(Map.ofEntries(");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    void should_request_the_model_fallback_when_a_removal_handler_has_multiple_keys(bool mixed)
    {
        var context = Render(slice => slice);
        var owner = ScopeSelection.Locate(context.Request.Model.Application).Single(owner => owner.Slice.ReadModels.Any(model => model.Name == "Metric"));
        var projection = owner.Slice.Projections.Single();
        var from = projection.Scope!.From[0];
        var remove = projection.Scope.Removals[0] with { EventContract = from.EventContract, Key = from.Key };
        var other = remove with { Key = new SemanticProjectionPartsKey([.. ((SemanticProjectionPartsKey)remove.Key).Parts.Select((part, index) => index == 0 ? part with { Value = new SemanticProjectionLiteral(new SemanticTextValue("other")) } : part)]) };

        // Exercise the sequencing guard directly: today's semantic validator rejects repeated
        // event contracts on one scoped level before they can reach the emitter.
        var writer = projection with { Scope = projection.Scope with { From = mixed ? [from] : [], Removals = mixed ? [other] : [remove, other] } };
        new ProjectionSequencing(context, new(context.Request.Model.Application, "axonviews"), owner.Slice.ReadModels.Single()).NeedsFallback(writer).ShouldBeTrue();
    }
}
