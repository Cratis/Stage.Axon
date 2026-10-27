// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Automations;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonAutomationsEmitter;

public class when_producing_alongside_an_unsupported_trigger : given.a_reaction_model
{
    void Establish()
    {
        Produce();
        var probe = new AxonEmissionContext(request, new(request.Profile));
        var input = new ReactionInput(string.Empty, [], null);
        var invoked = ScopeSelection.Select(request).SelectMany(located => located.Slice.Commands).First(candidate => ReactionInvocation.Render(probe, Invocation(candidate), input) is not null);
        var schedule = new SemanticReactionTrigger(SemanticReactionTriggerKind.Interval) { Every = 60, Invokes = [Invocation(invoked)] };
        reaction = reaction with { RunsAs = new(SemanticReactionIdentityKind.System, ["Sales"]), Triggers = [reaction.Triggers[0], schedule] };
    }

    void Because() => Render();

    [Fact] void should_not_register_the_reaction() => Code("SubmitOrderReaction").ShouldNotContain("@Component");
    [Fact] void should_not_emit_the_append_command() => context.Artifacts.Any(artifact => artifact.RelativePath.EndsWith("/SubmitOrderOn0Append.java", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_reference_the_suppressed_append_command() => Code("SubmitOrderReaction").ShouldNotContain("SubmitOrderOn0Append");
    [Fact] void should_leave_the_producing_trigger_as_unsupported_intent() => Code("SubmitOrderReaction").ShouldContain("trigger 1 with its original guards");

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
}
