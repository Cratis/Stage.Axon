// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Automations;
using Cratis.Stage.Rendering.Axon.Emitters.Events;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonAutomationsEmitter;

public class when_producing_into_another_module : given.a_reaction_model
{
    string ownerPackage;

    void Because()
    {
        Produce();
        Render();
        var application = request.Model.Application;
        var automation = ScopeSelection.Select(request).Single(slice => slice.Slice.Reactions.Any(candidate => candidate.Id == reaction.Id));
        var target = reaction.Triggers[0].Produces[0].EventContract;
        ownerPackage = AxonEventsEmitter.Package(context, ScopeSelection.Select(request).First(slice => slice.Slice.Events.Any(contract => contract.Id == target)).Module);
        var module = automation.Module with
        {
            Id = SemanticId.Create(SemanticKind.Module, "Automation"),
            Name = "Automation",
            Features = [automation.Features[^1] with { Id = SemanticId.Create(SemanticKind.Feature, "Automation.SubmitOrder"), Name = "SubmitOrder", Features = [], Slices = [automation.Slice] }]
        };
        var modules = application.Modules.Select(module => module with { Features = [.. module.Features.Select(RemoveAutomation)] }).Append(module);
        request = request with { Model = ExecutableSemanticModel.Create(request.Model.LanguageVersion, request.Model.SemanticVersion, application with { Modules = [.. modules] }) };
        context = new(request, new(request.Profile));
        new AxonAutomationsEmitter().Emit(context);
        new EventTagsEmitter().Emit(context);
    }

    [Fact] void should_reference_the_produced_events_module() => Code("SubmitOrderOn0AppendStream").ShouldContain($"import {ownerPackage}.EventTags;");
    [Fact] void should_reference_the_short_tag_name() => Code("SubmitOrderOn0AppendStream").ShouldContain("tagKey = EventTags.ORDER_ID");
    [Fact] void should_not_reference_the_automation_module() => Code("SubmitOrderOn0AppendStream").ShouldNotContain($"{context.BasePackage}.{JavaNames.PackageSegment("Automation")}.events.EventTags");
    [Fact] void should_emit_the_target_modules_constants() => context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("/orders/events/EventTags.java", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_not_emit_constants_for_the_eventless_automation_module() => context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("/automation/events/EventTags.java", StringComparison.Ordinal)).ShouldBeFalse();

    SemanticFeature RemoveAutomation(SemanticFeature feature) => feature with
    {
        Features = [.. feature.Features.Select(RemoveAutomation)],
        Slices = [.. feature.Slices.Where(slice => !slice.Reactions.Any(candidate => candidate.Id == reaction.Id))]
    };
}
