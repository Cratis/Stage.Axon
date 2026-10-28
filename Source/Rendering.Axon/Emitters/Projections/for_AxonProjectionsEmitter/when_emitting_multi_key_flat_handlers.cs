// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_emitting_multi_key_flat_handlers : Persistence.for_PersistentViewAdmission.given.a_flat_view
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    void should_reserve_both_evaluated_keys_before_applying_transitions(bool root)
    {
        var code = Projector(root);
        code.ShouldContain("ReadModelTransaction.reserve(bound, List.of(\"global\", String.valueOf(event.amount())));");
        var reservation = code.IndexOf("ReadModelTransaction.reserve(", StringComparison.Ordinal);
        var invocation = root ? "apply(event, message, updates, bound);" : "apply(event, updates, bound);";
        code.ShouldContain(invocation);
        var apply = code.IndexOf(invocation, StringComparison.Ordinal);
        (reservation < apply).ShouldBeTrue();
        code.ShouldContain("store.update(\"global\", current -> {");
        code.ShouldContain("store.update(String.valueOf(event.amount()), current -> {");
    }

    [Fact]
    void should_leave_the_persistent_branch_on_the_context_bound_emitter()
    {
        var code = Projector(false);
        code.ShouldContain("if (store instanceof JpaReadModelStore<?>) {\n        apply(event, updates, JpaReadModelStore.bind(store, context));\n    }");
        code.ShouldNotContain("runOnAfterCommit");
        code.ShouldContain("else {\n        var bound = ReadModelTransaction.bind(store, context);");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    void should_sequence_multi_key_handlers_by_the_model_namespace(bool root)
    {
        var code = Projector(root);
        code.ShouldContain("return Optional.of(\"axonviews.views.counters.totals.Total\");");
        code.ShouldNotContain("return Optional.of(String.valueOf(event.eventSourceId()");
    }

    string Projector(bool root)
    {
        var context = new AxonEmissionContext(_request, new(_request.Profile));
        var slice = ScopeSelection.Locate(_request.Model.Application).Single(owner => owner.Slice.ReadModels.Any(model => model.Id == ReadModelId("Total"))).Slice;
        var model = slice.ReadModels.Single(model => model.Id == ReadModelId("Total"));
        var projection = slice.Projections.Single(projection => projection.ReadModel == model.Id);
        var @event = ScopeSelection.Locate(_request.Model.Application).SelectMany(owner => owner.Slice.Events).Single(@event => @event.Id == projection.Scope!.From[0].EventContract);
        var first = projection.Scope!.From[0];
        var second = first with { Key = new SemanticProjectionValueKey(new SemanticProjectionEventProperty([@event.Properties.Single(property => property.Name == "amount").Id])) };
        var types = new ViewJavaTypes(_request.Model.Application, JavaNames.PackageSegment(_request.Model.Application.Name));
        var body = new JavaCodeBuilder("axonviews.features.totals.views");

        // The semantic validator currently permits only one from per event on a level; exercise multi-key emission directly.
        projection = projection with { Scope = projection.Scope with { From = [first, second] } };
        if (root)
        {
            new RootProjectionEmitter(context, types, "axonviews.features.totals.views", projection, model).Emit();
            return Code(context, "TotalProjectionProjector");
        }
        var values = new ProjectionValues(context, types, body);
        var keys = projection.Scope.From.Select(from => values.Key(from.Key, @event)!).ToArray();
        new ProjectionHandlerBinding(context, types, model, "axonviews.features.totals.views", keys: keys, sequence: new(new ProjectionPolicyNames(context.Request.Model.Application, [@event.Id]).For(@event.Id), types.EventType(@event.Id, body), null)).Emit(body, types.EventType(@event.Id, body), method =>
        {
            foreach (var key in keys) method.Line($"store.update({key}, current -> {{").Line("    return current;").Line("});");
        });
        return body.Build();
    }
}
