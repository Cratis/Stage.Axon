// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;
using Cratis.Stage.Rendering.Axon.Specifications.Views;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_emitting_row_sequencing : Persistence.for_PersistentViewAdmission.given.a_flat_view
{
    AxonEmissionContext _context;

    void Because() => _context = Render(slice => slice);

    [Fact] void should_attach_the_policy_to_the_discovered_handler() => Code(_context, "TotalProjectionProjector").ShouldContain("@SequencingPolicy(type = IncreasedSequence.class)\n    @EventHandler\n    public void handle(");
    [Fact]
    void should_return_the_literal_row_key()
    {
        var code = Code(_context, "TotalProjectionProjector");
        code.ShouldContain("store.update(\"global\", current -> {");
        code.ShouldContain("return Optional.of(\"global\");");
    }
    [Fact] void should_convert_payloads_through_the_event_converter() => Code(_context, "TotalProjectionProjector").ShouldContain("message.payloadAs(Increased.class, context.component(EventConverter.class))");
    [Fact] void should_keep_the_on_method_context_free() => Code(_context, "TotalProjectionProjector").ShouldContain("public void on(Increased event, QueryUpdateEmitter updates)");
    [Fact]
    void should_sequence_placed_reducers_by_their_existing_source_key()
    {
        var code = Code(_context, "RebuiltTotalReducerProjector");
        code.ShouldContain("var key = String.valueOf(event.counterId().value());");
        code.ShouldContain("return Optional.of(String.valueOf(event.counterId().value()));");
    }
    [Fact] void should_leave_stateful_projectors_without_a_policy() => Code(_context, "BoardProjectionProjector").ShouldNotContain("@SequencingPolicy");
    [Fact] void should_route_admitted_part_key_removals() => Code(_context, "MetricProjectionProjector").ShouldContain("@SequencingPolicy(type = MetricRemovedSequence.class)");
    [Fact] void should_route_the_rollback_witness_like_the_projector() => Code(_context, "TotalPersistentSupport").ShouldContain("@SequencingPolicy(type = TotalProjectionProjector.IncreasedSequence.class)");

    [Fact]
    void should_use_the_evaluated_property_key()
    {
        var contract = ScopeSelection.Locate(_request.Model.Application).SelectMany(owner => owner.Slice.Events).Single(contract => contract.Name == "Increased");
        Policy(new SemanticProjectionValueKey(new SemanticProjectionEventProperty([contract.Properties.Single(property => property.Name == "amount").Id])))
            .ShouldContain("return Optional.of(String.valueOf(event.amount()));");
    }

    [Fact] void should_stringify_boolean_literal_keys() => Policy(new SemanticProjectionValueKey(new SemanticProjectionLiteral(new SemanticBooleanValue(true)))).ShouldContain("return Optional.of(\"true\");");
    [Fact] void should_stringify_number_literal_keys() => Policy(new SemanticProjectionValueKey(new SemanticProjectionLiteral(new SemanticNumberValue(42)))).ShouldContain("return Optional.of(\"42\");");
    [Fact] void should_conservatively_route_multi_key_handlers() => Policy(null).ShouldContain("return Optional.of(\"axonviews.views.counters.totals.Total\");");

    [Theory]
    [InlineData("Total", true)]
    [InlineData("RebuiltTotal", false)]
    void should_override_only_default_event_source_identities(string name, bool overridden)
    {
        var owner = ScopeSelection.Locate(_request.Model.Application).Single(owner => owner.Slice.ReadModels.Any(model => model.Name == name));
        var model = owner.Slice.ReadModels.Single(model => model.Name == name);
        var given = owner.Slice.Specifications.First(spec => spec.ThenReadModels.Any(expected => expected.ReadModel == model.Id)).GivenEvents[0];
        var expression = AxonViewEvents.Expression(_context, new(_request.Model.Application, "axonviews"), given, source: "\"alternate-source\"");
        expression.Contains("\"alternate-source\"", StringComparison.Ordinal).ShouldEqual(overridden);
    }

    string Policy(SemanticProjectionKey? key)
    {
        var owner = ScopeSelection.Select(_request).Single(owner => owner.Slice.ReadModels.Any(model => model.Name == "Total"));
        var model = owner.Slice.ReadModels.Single();
        var contract = ScopeSelection.Locate(_request.Model.Application).SelectMany(owner => owner.Slice.Events).Single(contract => contract.Name == "Increased");
        var types = new ViewJavaTypes(_request.Model.Application, "axonviews");
        var body = new JavaCodeBuilder(types.Package(owner));
        var values = new ProjectionValues(_context, types, body);
        new ProjectionHandlerBinding(_context, types, model, types.Package(owner), sequence: new(new ProjectionPolicyNames(_context.Request.Model.Application, [contract.Id]).For(contract.Id), types.EventType(contract.Id, body), key is null ? null : values.Key(key, contract))).Emit(body, types.EventType(contract.Id, body), _ => { });
        return body.Build();
    }

    [Fact]
    void should_use_the_existing_source_key_expression()
    {
        var context = Render(slice => slice with
        {
            Projections = [.. slice.Projections.Select(projection => projection.ReadModel != ReadModelId("Total") ? projection : projection with
            {
                Scope = projection.Scope with
                {
                    From = [.. projection.Scope.From.Select(transition => transition with { Key = new SemanticProjectionValueKey(new SemanticProjectionEventSourceIdentity()) })]
                }
            })]
        });
        Code(context, "TotalProjectionProjector").ShouldContain("store.update(String.valueOf(event.eventSourceId().value()), current -> {");
        Code(context, "TotalProjectionProjector").ShouldContain("return Optional.of(String.valueOf(event.eventSourceId().value()));");
        context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("/TotalPersistentRowOrderTest.java", StringComparison.Ordinal)).ShouldBeFalse();
    }
}
