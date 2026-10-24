// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter;

public class when_enlisting_property_uniqueness : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    ArtifactRenderPlan plan;
    string handler;
    string constraints;

    void Because()
    {
        var slices = ScopeSelection.Select(request).ToArray();
        var command = slices.SelectMany(slice => slice.Slice.Commands).Single(command => command.Name == "PlaceOrder");
        var contract = slices.SelectMany(slice => slice.Slice.Events).Single(contract => contract.Id == command.Produces[0].EventContract);
        var release = slices.SelectMany(slice => slice.Slice.Commands).Single(command => command.Name == "MoveOrder").Produces[0].EventContract;
        var property = contract.Properties.Single(property => property.Name == "customer");
        var constraint = new SemanticConstraint("CustomerUnique", SemanticConstraintKind.UniquePropertyValue, SemanticConstraintScope.EventSequence, [new(contract.Id, [property.Id])], [release], true, "Customer is already claimed");
        SemanticFeature Feature(SemanticFeature feature) => feature with { Features = [.. feature.Features.Select(Feature)], Slices = [.. feature.Slices.Select(slice => slice.Commands.Any(candidate => candidate.Id == command.Id) ? slice with { Constraints = [constraint] } : slice)] };
        var app = loaded.Model.Application with { Modules = [.. loaded.Model.Application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })] };
        var model = ExecutableSemanticModel.Create(loaded.Model.LanguageVersion, loaded.Model.SemanticVersion, app);
        request = new(model, Cratis.Screenplay.Semantics.Execution.SemanticExecutionPlan.Compile(model).Plan!, request.Profile, new(ArtifactRenderScopeKind.Application, app.Id));
        plan = new AxonArtifactRenderPlanner().Plan(request);
        handler = Source("PlaceOrderHandler.java");
        constraints = Source("ModeledConstraints.java");
    }

    string Source(string name) => Encoding.UTF8.GetString(plan.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name, StringComparison.Ordinal)).Bytes.AsSpan());

    [Fact] void should_enlist_before_appending() => (handler.IndexOf("ModeledConstraints.apply", StringComparison.Ordinal) < handler.IndexOf("appender.append", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_use_the_append_processing_context() => handler.ShouldContain("ModeledConstraints.apply(uniqueValues, event, context)");
    [Fact] void should_claim_the_modeled_property() => constraints.ShouldContain("java.util.Arrays.asList(fact.customer()), fact.orderId(), true, \"Customer is already claimed\"");
    [Fact] void should_release_by_owner_on_the_modeled_event() => constraints.ShouldContain("OrderMoved fact) store.releaseOwner");
    [Fact] void should_emit_a_persistent_store() => Source("JpaUniqueValueStore.java").ShouldContain("findTransactionalExecutor(context)");
    [Fact] void should_use_the_axon_transaction_callback() => Source("JpaUniqueValueStore.java").ShouldContain("ThrowingConsumer<EntityManager, Exception> action");
    [Fact] void should_release_in_memory_reservations_after_errors_too() => Source("InMemoryUniqueValueStore.java").ShouldContain("context.doFinally(completed -> complete(changes, completed.isCommitted()))");
    [Fact] void should_share_the_runtime_http_test_context() => Source("UniqueAppendTransactionTest.java").ShouldContain("@Import(RuntimeHttpRoundTripTest.EchoEndpoint.class)");
    [Fact] void should_make_the_restart_proof_opt_in() => Source("PersistentRestartTest.java").ShouldContain("@EnabledIfSystemProperty(named = \"stage.restart\", matches = \"true\")");
    [Fact] void should_boot_twice_on_the_same_database() => Source("PersistentRestartTest.java").ShouldContain("try (var second = boot(database))");
    [Fact] void should_construct_the_aggregate_jpa_engine() => Source("PersistentConfiguration.java").ShouldContain("new AggregateBasedJpaEventStorageEngine(");
    [Fact] void should_use_the_configured_event_converter() => Source("PersistentConfiguration.java").ShouldContain("configuration.getComponent(EventConverter.class)");
    [Fact] void should_tag_the_native_proof_event_with_the_owner() => Source("UniqueAppendTransactionTest.java").ShouldContain("@org.axonframework.eventsourcing.annotation.EventTag(key = \"owner\")");
    [Fact] void should_map_conflicts_to_http_409() => Source("ProblemDetailsAdvice.java").ShouldContain("HttpStatus.CONFLICT");
    [Fact] void should_not_refuse_supported_uniqueness() => plan.Diagnostics.Any(diagnostic => diagnostic.Code == "STAGE-AXON-0801").ShouldBeFalse();
}
