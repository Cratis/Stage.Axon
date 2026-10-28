// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_preserving_row_key_parity : Persistence.for_PersistentViewAdmission.given.a_flat_view
{
    AxonEmissionContext _context;

    void Because() => _context = Render(slice => slice);

    [Fact]
    void should_preserve_the_root_key_for_nested_mapping_targets()
    {
        var code = Code(_context, "ContextProjectionProjector");
        code.ShouldContain("state.withDetails(new CounterDetails(\"increased\"))");
        code.ShouldContain("store.update(\"global\", current -> {");
        code.ShouldContain("return Optional.of(\"global\");");
    }

    [Fact]
    void should_use_the_placed_reducer_store_key_without_a_second_conversion()
    {
        var code = Code(_context, "RebuiltTotalReducerProjector");
        code.ShouldContain("var key = String.valueOf(event.counterId().value());");
        code.ShouldContain("store.get(key).orElse(null)");
        code.ShouldContain("store.put(key, next)");
        code.ShouldContain("return Optional.of(String.valueOf(event.counterId().value()));");
    }

    [Theory]
    [InlineData(false, "String.valueOf(event.optionalName())")]
    [InlineData(true, "String.valueOf(event.optionalDetails().note())")]
    void should_preserve_nullable_path_keys_and_never_fall_back_to_the_source(bool nested, string key)
    {
        var owners = ScopeSelection.Locate(_request.Model.Application).ToArray();
        var details = owners.SelectMany(owner => owner.Slice.ReadModels).Single(model => model.Name == "ContextTotal").Properties.Single(property => property.Name == "details");
        var note = owners.SelectMany(owner => owner.Slice.ReadModels).Single(model => model.Name == "Total").Properties.Single(property => property.Name == "note");
        var property = (nested ? details : note) with { Id = SemanticId.Create(SemanticKind.Property, "Increased.optionalKey"), Name = nested ? "optionalDetails" : "optionalName" };
        var context = Render(slice => slice with { Specifications = [], Events = [.. slice.Events.Select(contract => contract.Name == "Increased" ? contract with { Properties = [.. contract.Properties, property] } : contract)] });
        var owner = ScopeSelection.Locate(context.Request.Model.Application).Single(owner => owner.Slice.ReadModels.Any(model => model.Name == "Total"));
        var model = owner.Slice.ReadModels.Single();
        var original = owner.Slice.Projections.Single();
        var path = nested ? new[] { property.Id, _request.Model.Application.Types.Single(type => type.Name == "CounterDetails").Properties.Single().Id } : [property.Id];
        var projection = original with { Scope = original.Scope with { From = [original.Scope.From[0] with { Key = new SemanticProjectionValueKey(new SemanticProjectionEventProperty([.. path])) }] } };
        var fresh = new AxonEmissionContext(context.Request, new(context.Request.Profile));
        new RootProjectionEmitter(fresh, new(context.Request.Model.Application, "axonviews"), "axonviews.views.counters.totals", projection, model).Emit();
        var code = Code(fresh, "TotalProjectionProjector");
        code.ShouldContain($"store.update({key}, current -> {{");
        code.ShouldContain($"return Optional.of({key});");

        // A null leaf retains String.valueOf's "null" key; a null intermediate retains the store's failure.
        code.ShouldNotContain("Optional.ofNullable");
        code.ShouldNotContain("return Optional.of(\"global\");");
        code.ShouldNotContain("return Optional.of(String.valueOf(event.eventSourceId()");
    }
}
