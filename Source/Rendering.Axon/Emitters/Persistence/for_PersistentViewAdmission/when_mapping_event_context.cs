// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence.for_PersistentViewAdmission;

public class when_mapping_event_context : given.a_flat_view
{
    readonly Dictionary<string, AxonEmissionContext> _contexts = new(StringComparer.Ordinal);

    void Because()
    {
        foreach (var path in new[] { "occurred", "correlationId", "eventSourceId", "sequenceNumber" })
        {
            _contexts[path] = Render(slice => slice with
            {
                Specifications = [.. slice.Specifications.Select(spec => spec with { ThenReadModels = [.. spec.ThenReadModels.Select(expected => expected with { Values = [.. expected.Values.Where(value => !slice.ReadModels.Where(model => model.Name == "Total").SelectMany(model => model.Properties).Any(property => property.Name == "note" && property.Id == value.TargetProperty))] })] })],
                ReadModels = [.. slice.ReadModels.Select(model => model.Name == "Total" ? model with { Properties = [.. model.Properties.Select(property => property.Name == "note" ? property with { Type = SemanticTypeReference.ForPrimitive(path switch { "occurred" => SemanticPrimitiveType.DateTime, "correlationId" => SemanticPrimitiveType.Uuid, "sequenceNumber" => SemanticPrimitiveType.WholeNumber, _ => SemanticPrimitiveType.Text }) } : property)] } : model)],
                Projections = [.. slice.Projections.Select(projection => projection.ReadModel == ReadModelId("Total") ? projection with
                {
                    Scope = projection.Scope! with { From = [projection.Scope.From[0] with { Mappings = [projection.Scope.From[0].Mappings.First(mapping => mapping.Target[0] == slice.ReadModels.Single(model => model.Name == "Total").Properties.Single(property => property.Name == "note").Id) with { Source = new SemanticProjectionEventContextValue(path) }] }] }
                } : projection)]
            });
        }
    }

    [Fact] void should_admit_occurred() => PersistentViewAdmission.Supports(_contexts["occurred"], ReadModelId("Total")).ShouldBeTrue();
    [Fact] void should_admit_correlation() => PersistentViewAdmission.Supports(_contexts["correlationId"], ReadModelId("Total")).ShouldBeTrue();
    [Fact] void should_refuse_valid_but_unsupported_context() => PersistentViewAdmission.Supports(_contexts["sequenceNumber"], ReadModelId("Total")).ShouldBeFalse();
    [Fact] void should_warn_for_valid_but_unsupported_context() => Code(_contexts["sequenceNumber"], "TotalStoreConfiguration").ShouldContain("STAGE-AXON-0308");
    [Fact] void should_admit_event_source() => PersistentViewAdmission.Supports(_contexts["eventSourceId"], ReadModelId("Total")).ShouldBeTrue();
    [Fact] void should_refuse_unknown_context() => PersistentViewAdmission.Context(new SemanticProjectionEventContextValue("unknown")).ShouldBeFalse();
    [Fact] void should_keep_the_scoped_context_guard() => Code(_contexts["occurred"], "BoardStoreConfiguration").ShouldContain("persistentReadModelSafety");
    [Fact] void should_keep_scoped_views_refused() => PersistentViewAdmission.Supports(_contexts["occurred"], ReadModelId("Board")).ShouldBeFalse();
    [Fact] void should_use_the_delivered_timestamp() => Projector.ShouldContain("message.timestamp()");
    [Fact] void should_warn_for_every_context_path_without_crashing() => _contexts.Values.ShouldEachConformTo(context => Code(context, "TotalStoreConfiguration").Contains("STAGE-AXON-0308", StringComparison.Ordinal));
    [Fact] void should_report_the_warning() => _contexts.Values.ShouldEachConformTo(context => context.Degradation.UnsupportedConstructs.Concat(context.Degradation.Warnings).Any(diagnostic => diagnostic.Artifact == ReadModelId("Total") && diagnostic.Code == "STAGE-AXON-0308"));
    [Fact] void should_keep_the_memory_store() => _contexts.Values.ShouldEachConformTo(context => Code(context, "TotalStoreConfiguration").Contains("InMemoryReadModelStore", StringComparison.Ordinal));
    [Fact] void should_not_emit_persistent_proofs_for_unsupported_context() => _contexts.Where(pair => pair.Key == "sequenceNumber").ShouldEachConformTo(pair => !pair.Value.Artifacts.Exists(artifact => artifact.RelativePath.Contains("/TotalPersistent", StringComparison.Ordinal)));
    [Fact] void should_not_guard_an_admitted_persistent_view() => Code(_contexts["occurred"], "TotalStoreConfiguration").ShouldNotContain("persistentReadModelSafety");

    [Fact] void should_explain_unsupported_event_context() => Code(_contexts["sequenceNumber"], "TotalStoreConfiguration").ShouldContain("event-context value");

    string Projector => Code(_contexts["occurred"], "TotalProjectionProjector");
}
