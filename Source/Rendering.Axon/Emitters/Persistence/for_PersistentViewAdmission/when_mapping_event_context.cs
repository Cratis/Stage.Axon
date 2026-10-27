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
    [Fact] void should_bind_the_contextual_store() => Projector.ShouldContain("JpaReadModelStore.bind(store, context)");
    [Fact] void should_handle_with_only_the_emitter_and_processing_context() => Projector.ShouldContain(" event, QueryUpdateEmitter updates, ProcessingContext context)");
    [Fact] void should_resolve_the_message_from_the_processing_context() => Projector.ShouldContain("var message = (EventMessage) Message.fromContext(context);");
    [Fact] void should_not_resolve_the_message_as_a_handler_parameter() => Projector.ShouldNotContain("EventMessage message, QueryUpdateEmitter updates, ProcessingContext context)");
    [Fact] void should_use_the_delivered_timestamp() => Projector.ShouldContain("message.timestamp()");
    [Fact] void should_not_keep_projection_keys() => Projector.ShouldNotContain("Set<Object> keys");
    [Fact] void should_not_keep_pending_events() => Projector.ShouldNotContain("pending");
    [Fact] void should_normalize_context_fields_in_the_proof() => Code(_contexts["occurred"], "TotalPersistentSupport").ShouldContain("row = new Total(row.name(), row.amount(), row.count(), null);");
    [Fact] void should_pass_the_message_placeholder_in_the_proof() => Code(_contexts["occurred"], "TotalPersistentSupport").ShouldContain(", null, null);");
    [Fact] void should_emit_enabled_tests() => Code(_contexts["occurred"], "TotalPersistentTest").ShouldNotContain("@Disabled");
    [Fact] void should_remove_the_admitted_guard() => Code(_contexts["occurred"], "TotalStoreConfiguration").ShouldNotContain("persistentReadModelSafety");

    string Projector => Code(_contexts["occurred"], "TotalProjectionProjector");
}
