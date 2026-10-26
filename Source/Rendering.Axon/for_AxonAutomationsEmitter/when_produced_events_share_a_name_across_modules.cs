// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Automations;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonAutomationsEmitter;

public class when_produced_events_share_a_name_across_modules : given.a_reaction_model
{
    SemanticId _movedEvent;

    void Because()
    {
        Produce();
        Render();
        var application = request.Model.Application;
        var slices = ScopeSelection.Locate(application).ToArray();
        var target = reaction.Triggers[0].Produces[0].EventContract;
        var produced = slices.SelectMany(slice => slice.Slice.Events).First(contract => contract.Id == target);
        var other = slices.First(slice => slice.Slice.Events.Any(contract => contract.Id != target));
        var moved = other.Slice.Events.First(contract => contract.Id != target) with { Name = produced.Name };
        _movedEvent = moved.Id;
        var slice = other.Slice with
        {
            Id = SemanticId.Create(SemanticKind.Slice, "Other.Events"),
            Name = "Events",
            Events = [moved],
            Commands = [],
            Reactions = [],
            Constraints = [],
            ReadModels = [],
            Projections = [],
            Queries = [],
            Specifications = [],
            Reducers = [],
            Captures = []
        };
        var feature = other.Features[^1] with { Id = SemanticId.Create(SemanticKind.Feature, "Other.Events"), Name = "Events", Features = [], Slices = [slice] };
        var module = other.Module with { Id = SemanticId.Create(SemanticKind.Module, "Other"), Name = "Other", Features = [feature] };
        application = application with { Modules = [.. application.Modules.Select(candidate => candidate with { Features = [.. candidate.Features.Select(RemoveMovedEvent)] }), module] };
        request = request with { Model = ExecutableSemanticModel.Create(request.Model.LanguageVersion, request.Model.SemanticVersion, application) };
        context = new(request, new(request.Profile));
        new AxonAutomationsEmitter().Emit(context);
    }

    [Fact] void should_import_the_first_modules_event() => Code("SubmitOrderOn0AppendStream").ShouldContain("import sample.orders.events.OrderPlaced;");
    [Fact] void should_emit_a_handler_for_the_imported_event() => Code("SubmitOrderOn0AppendStream").ShouldContain("(OrderPlaced event)");
    [Fact] void should_qualify_the_other_modules_event() => Code("SubmitOrderOn0AppendStream").ShouldContain("(sample.other.events.OrderPlaced event)");
    [Fact] void should_not_import_the_other_modules_event() => Code("SubmitOrderOn0AppendStream").ShouldNotContain("import sample.other.events.OrderPlaced;");

    SemanticFeature RemoveMovedEvent(SemanticFeature feature) => feature with
    {
        Features = [.. feature.Features.Select(RemoveMovedEvent)],
        Slices = [.. feature.Slices.Select(slice => slice with { Events = [.. slice.Events.Where(contract => contract.Id != _movedEvent)] })]
    };
}
