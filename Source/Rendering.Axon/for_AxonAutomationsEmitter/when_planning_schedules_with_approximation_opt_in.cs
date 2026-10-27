// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Automations;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonAutomationsEmitter;

public class when_planning_schedules_with_approximation_opt_in : given.a_reaction_model
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
    void should_not_register_the_schedule_and_warn_with_a_comment_without_opt_in()
    {
        Emit(request.Profile);
        Registered().ShouldBeFalse();
        context.Degradation.UnsupportedConstructs.Single(diagnostic => diagnostic.Code == Code0507).Severity.ShouldEqual(ArtifactRenderDiagnosticSeverity.Warning);
        context.Degradation.Intents.Any(intent => intent.Code == Code0507).ShouldBeTrue();
        Text().ShouldContain("STAGE-AXON-0504");
    }

    [Fact]
    void should_refuse_the_schedule_without_opt_in_in_strict_mode()
    {
        Emit(AxonRendering.CreateProfile(strict: true));
        Registered().ShouldBeFalse();
        context.Degradation.Refusals.Any(refusal => refusal.Codes.Contains(Code0507)).ShouldBeTrue();
    }

    [Fact]
    void should_register_the_schedule_with_a_warning_and_no_refusal_with_opt_in()
    {
        Emit(AxonRendering.AllowApproximations(AxonRendering.CreateProfile(strict: true), Code0507));
        Registered().ShouldBeTrue();
        context.Degradation.UnsupportedConstructs.Single(diagnostic => diagnostic.Code == Code0507).Severity.ShouldEqual(ArtifactRenderDiagnosticSeverity.Warning);
        context.Degradation.Refusals.ShouldBeEmpty();
    }

    bool Registered() => Text().Contains("fixedRateString", StringComparison.Ordinal);

    string Text() => string.Concat(context.Artifacts.Select(artifact => Encoding.UTF8.GetString(artifact.Bytes.AsSpan())));

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

    void Emit(ArtifactRenderProfile profile)
    {
        reaction = reaction with { Triggers = [trigger] };
        request = request with { Profile = profile };
        Render();
    }
}
