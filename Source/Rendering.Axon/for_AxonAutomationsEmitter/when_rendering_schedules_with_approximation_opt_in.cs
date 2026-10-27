// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Automations;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonAutomationsEmitter;

public class when_rendering_schedules_with_approximation_opt_in : given.a_reaction_model
{
    const string Code0507 = "STAGE-AXON-0507";

    SemanticReactionTrigger trigger;

    void Establish()
    {
        var probe = new AxonEmissionContext(request, new(request.Profile));
        var input = new ReactionInput(string.Empty, [], null);
        var invoked = ScopeSelection.Select(request).SelectMany(located => located.Slice.Commands).First(candidate => ReactionInvocation.Render(probe, Invocation(candidate), input) is not null);
        reaction = reaction with { RunsAs = new(SemanticReactionIdentityKind.System, ["Sales"]) };
        trigger = new(SemanticReactionTriggerKind.Interval) { Every = 60, Invokes = [Invocation(invoked)] };
    }

    [Fact]
    void should_not_prepare_the_schedule_without_opt_in()
    {
        Prepare(request.Profile).ShouldBeNull();
        context.Degradation.UnsupportedConstructs.Single(diagnostic => diagnostic.Code == Code0507).Severity.ShouldEqual(ArtifactRenderDiagnosticSeverity.Warning);
    }

    [Fact]
    void should_refuse_the_schedule_without_opt_in_in_strict_mode()
    {
        Prepare(AxonRendering.CreateProfile(strict: true)).ShouldBeNull();
        context.Degradation.Refusals.Single().Codes.ShouldContainOnly(Code0507);
    }

    [Fact]
    void should_prepare_the_schedule_with_opt_in_and_warn()
    {
        Prepare(AxonRendering.AllowApproximations(request.Profile, Code0507)).ShouldNotBeNull();
        context.Degradation.UnsupportedConstructs.Single(diagnostic => diagnostic.Code == Code0507).Severity.ShouldEqual(ArtifactRenderDiagnosticSeverity.Warning);
    }

    [Fact]
    void should_not_refuse_an_opted_in_schedule_in_strict_mode()
    {
        Prepare(AxonRendering.AllowApproximations(AxonRendering.CreateProfile(strict: true), Code0507)).ShouldNotBeNull();
        context.Degradation.Refusals.ShouldBeEmpty();
    }

    SemanticInvocation Invocation(SemanticCommand candidate) => new(candidate.Id, [.. candidate.Properties.Select(property => new SemanticPropertyMapping(property.Id, new SemanticValueExpression(Value(property))))]);

    SemanticValue Value(SemanticProperty property)
    {
        var concept = property.Type.Kind == SemanticTypeReferenceKind.Concept ? request.Model.Application.Concepts.Single(candidate => candidate.Id == property.Type.Target) : null;
        if (concept is { Values.IsDefaultOrEmpty: false }) return new SemanticTextValue(concept.Values[0]);
        return (concept?.Primitive ?? property.Type.Primitive) switch
        {
            SemanticPrimitiveType.Uuid => new SemanticTextValue("00000000-0000-0000-0000-000000000001"),
            SemanticPrimitiveType.Boolean => new SemanticBooleanValue(true),
            SemanticPrimitiveType.Text => new SemanticTextValue("x"),
            _ => new SemanticNumberValue(1)
        };
    }

    PreparedReactionTrigger? Prepare(ArtifactRenderProfile profile)
    {
        request = request with { Profile = profile };
        context = new(request, new(profile));
        return new ReactionTriggerEmitter(context, reaction).Prepare(trigger, 0);
    }
}
