// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence.for_PersistentViewAdmission;

public class when_admitting_part_key_removals : given.a_flat_view
{
    AxonEmissionContext _context;

    void Because() => _context = Render(slice => slice);

    [Fact] void should_admit_metric() => PersistentViewAdmission.Supports(_context, ReadModelId("Metric")).ShouldBeTrue();
    [Fact] void should_emit_the_durable_store() => Code(_context, "MetricStoreConfiguration").ShouldContain("return new JpaReadModelStore<>");
    [Fact] void should_keep_stateful_scopes_refused() => PersistentViewAdmission.Supports(_context, ReadModelId("Board")).ShouldBeFalse();
    [Fact] void should_emit_a_queryless_removal_proof() => Code(_context, "MetricPersistentRemovalTest").ShouldContain("class MetricPersistentRemovalTest");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    void should_refuse_empty_part_keys(bool removal)
    {
        var projection = Metric();
        var changed = projection with
        {
            Scope = projection.Scope with
            {
                From = removal ? projection.Scope.From : [.. projection.Scope.From.Select(transition => transition with { Key = new SemanticProjectionPartsKey([]) })],
                Removals = removal ? [.. projection.Scope.Removals.Select(remove => remove with { Key = new SemanticProjectionPartsKey([]) })] : projection.Scope.Removals
            }
        };
        ProjectionHandlerShape.RootOnly(changed).ShouldBeFalse();
    }

    [Fact]
    void should_keep_value_key_removals_refused()
    {
        var projection = Metric();
        ProjectionHandlerShape.RootOnly(projection with
        {
            Scope = projection.Scope with
            {
                Removals = [.. projection.Scope.Removals.Select(remove => remove with { Key = new SemanticProjectionValueKey(new SemanticProjectionEventSourceIdentity()) })]
            }
        }).ShouldBeFalse();
    }

    [Fact]
    void should_admit_a_separate_removal_only_writer()
    {
        var context = Render(slice => slice with
        {
            Specifications = [],
            Projections = [.. slice.Projections.SelectMany(projection => projection.ReadModel == ReadModelId("Metric")
                ? (SemanticProjection[])[projection with { Scope = projection.Scope with { Removals = [] } }, projection with
                {
                    Id = SemanticId.Create(SemanticKind.Projection, "MetricRemoval"), Name = "MetricRemoval",
                    Scope = projection.Scope with { From = [] }
                }]
                : [projection])]
        });
        PersistentViewAdmission.Supports(context, ReadModelId("Metric")).ShouldBeTrue();
        Code(context, "MetricRemovalProjector").ShouldContain("store.remove(Map.ofEntries(");
        Code(context, "MetricRemovalProjector").ShouldContain("return Optional.of(Map.ofEntries(");
    }

    SemanticProjection Metric() => ScopeSelection.Locate(_request.Model.Application).SelectMany(owner => owner.Slice.Projections).Single(projection => projection.ReadModel == ReadModelId("Metric"));
}
