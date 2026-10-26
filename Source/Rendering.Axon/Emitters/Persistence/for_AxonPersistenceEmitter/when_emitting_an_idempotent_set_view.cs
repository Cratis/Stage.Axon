// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Queries;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence.for_AxonPersistenceEmitter;

public class when_emitting_an_idempotent_set_view : Projections.for_AxonProjectionsEmitter.given.a_view_request
{
    AxonEmissionContext _context;
    string _proof;
    string _support;
    string _dispatch;

    void Establish()
    {
        var owner = Sdk.ScopeSelection.Select(_request).Single(slice => slice.Slice.ReadModels.Any(model => model.Name == "Total"));
        var expected = owner.Slice.Specifications[0].ThenReadModels.Single();
        SemanticFeature Feature(SemanticFeature feature) => feature with
        {
            Features = [.. feature.Features.Select(Feature)],
            Slices = [.. feature.Slices.Select(slice => slice with
            {
                Projections = [.. slice.Projections.Select(projection => projection.ReadModel == ReadModelId("Total") ? projection with
                {
                    Scope = projection.Scope! with
                    {
                        From = [projection.Scope.From[0] with { Mappings = [.. expected.Values.Select(value => new SemanticProjectionMapping([value.TargetProperty], SemanticProjectionOperation.Set, new SemanticProjectionLiteral(value.Value)))] }]
                    }
                } : projection)]
            })]
        };
        var application = _request.Model.Application;
        _request = _request with { Model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application with { Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })] }) };
    }

    void Because()
    {
        _context = new(_request, new(_request.Profile));
        new AxonProjectionsEmitter().Emit(_context);
        new AxonQueriesEmitter().Emit(_context);
        new AxonPersistenceEmitter().Emit(_context);
        _proof = Code("TotalPersistentTest");
        _support = Code("TotalPersistentSupport");
        _dispatch = Code("TotalPersistentDefaultDispatchTest");
    }

    [Fact] void should_admit_the_set_only_projection() => PersistentViewAdmission.Supports(_context, ReadModelId("Total")).ShouldBeTrue();
    [Fact] void should_use_only_set_mappings() => Sdk.ScopeSelection.Select(_request).SelectMany(owner => owner.Slice.Projections).Where(projection => projection.ReadModel == ReadModelId("Total")).SelectMany(ProjectionTransitions.From).SelectMany(from => from.Mappings).All(mapping => mapping.Operation == SemanticProjectionOperation.Set).ShouldBeTrue();
    [Fact] void should_emit_the_qualifying_snapshot_query() => Code("AllTotalsHandler").ShouldContain("public AllTotals.Result handle(");
    [Fact] void should_emit_idempotent_modeled_state() => Code("TotalProjectionProjector").ShouldContain("state.withAmount(8L)");
    [Fact] void should_not_require_identical_replay_to_mutate_a_set() => _proof.ShouldNotContain("assertNotEquals(new HashSet<>(initial)");
    [Fact] void should_derive_replay_expectations_from_the_arrangement() => _support.ShouldContain("for (var replay = 0; replay < replays; replay++) projector.on(");
    [Fact] void should_expect_a_second_replay_after_reclaim() => _proof.ShouldContain("var subsequent = next(2);");
    [Fact] void should_use_a_distinct_durable_counter_view() => _support.ShouldContain("axonviews.views.counters.totals.Total.atomicity");
    [Fact] void should_enlist_the_counter_handler_in_the_same_processor() => _support.ShouldContain("@org.axonframework.messaging.core.annotation.Namespace(\"axonviews.views.counters.totals\")");
    [Fact] void should_require_an_actual_counter_change_inside_the_handler() => _support.ShouldContain("assertEquals(before + 1, changing.get(\"count\").orElseThrow()");
    [Fact] void should_require_the_counter_change_to_roll_back() => _proof.ShouldContain("assertEquals(1, count(application), \"Non-idempotent view change must roll back\")");
    [Fact] void should_require_real_token_store_rejection() => _proof.ShouldContain("assertTrue(tokenRejected.await(20, TimeUnit.SECONDS)");
    [Fact] void should_wait_for_a_changed_row_in_both_atomicity_proofs() => _proof.ShouldContain("assertTrue(viewChanged.await(20, TimeUnit.SECONDS)");
    [Fact] void should_require_exactly_once_counter_reclaim() => _proof.ShouldContain("count(application) == 1 + 1");
    [Fact] void should_dispatch_a_real_default_profile_event() => _dispatch.ShouldContain("EventAppender.forContext(context).append(new axonviews.views.events.Increased(");
    [Fact] void should_import_the_pinned_default_event_appender() => _dispatch.ShouldContain("import org.axonframework.messaging.eventhandling.gateway.EventAppender;");
    [Fact] void should_import_the_pinned_persistent_event_appender() => _support.ShouldContain("import org.axonframework.messaging.eventhandling.gateway.EventAppender;");
    [Fact] void should_not_import_the_invalid_persistent_event_appender() => _support.ShouldNotContain("org.axonframework.eventsourcing.eventstore.EventAppender");
    [Fact] void should_not_import_the_invalid_default_event_appender() => _dispatch.ShouldNotContain("org.axonframework.eventsourcing.eventstore.EventAppender");
    [Fact] void should_not_append_directly_through_the_event_store() => _dispatch.ShouldNotContain("eventStore.appendEvent(");
    [Fact] void should_not_construct_a_generic_event_message() => _dispatch.ShouldNotContain("GenericEventMessage");
    [Fact] void should_return_a_future_from_the_default_unit_of_work() => _dispatch.ShouldContain("return CompletableFuture.<Void>completedFuture(null);");
    [Fact] void should_import_the_default_callback_future() => _dispatch.ShouldContain("import java.util.concurrent.CompletableFuture;");
    [Fact] void should_not_return_a_plain_string_from_the_unit_of_work() => _dispatch.ShouldNotContain("return \"appended\";");
    [Fact] void should_not_invoke_the_projector_directly_in_the_default_proof() => _dispatch.ShouldNotContain("projector.on(");
    [Fact] void should_not_invoke_the_contextual_handler_directly_in_the_default_proof() => _dispatch.ShouldNotContain(".handle(");
    [Fact] void should_keep_the_default_fixture_out_of_component_scanning() => _dispatch.ShouldContain("@TestConfiguration(proxyBeanMethods = false)");
    [Fact] void should_import_the_actual_default_projector() => _dispatch.ShouldContain("axonviews.views.counters.totals.TotalProjectionProjector.class");
    [Fact] void should_assert_single_handling_even_for_idempotent_sets() => _dispatch.ShouldContain("assertEquals(1, store.writes.get(),");
    [Fact] void should_assert_one_result_row() => _dispatch.ShouldContain("assertEquals(1, store.all().size(),");
    [Fact] void should_assert_invalidation_before_append_completes() => _dispatch.ShouldContain("Subscribing default processing must invalidate before append completes");
    [Fact] void should_assert_invalidation_follows_the_write() => _dispatch.ShouldContain("assertEquals(1, writesAtInvalidation.get(),");
    [Fact] void should_keep_jpa_opt_in() => _dispatch.ShouldContain("\"spring.profiles.active\", \"default\"");

    string Code(string name) => Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith('/' + name + ".java", StringComparison.Ordinal)));
}
