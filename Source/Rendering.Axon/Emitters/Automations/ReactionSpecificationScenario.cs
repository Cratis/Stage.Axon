// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Automations;

internal sealed class ReactionSpecificationScenario(AxonEmissionContext context, SemanticSpecification specification, int index)
{
    readonly string? _input = new ReactionSpecificationValues(context).Trigger(context.Request.Model.Application.Triggers.Single(candidate => candidate.Id == specification.WhenTrigger!.Trigger), specification.WhenTrigger!);
    readonly string?[] _expected = [.. specification.ThenEvents.Select(new ReactionSpecificationValues(context).Event)];

    internal bool CanEmit => FixturesSupported() && TargetSupported() && _input is not null && _expected.All(value => value is not null);

    internal void Emit(JavaCodeBuilder test)
    {
        CommandFixtureSecurity.Caller(test, specification.GivenCaller);
        test.Line($"org.mockito.Mockito.when(clock.instant()).thenReturn(java.time.Instant.parse({JavaCodeBuilder.StringLiteral(specification.GivenClock ?? "1970-01-01T00:00:00Z")}));")
            .Line("org.mockito.Mockito.when(clock.getZone()).thenReturn(java.time.ZoneOffset.UTC);")
            .Line($"reaction.on{index}({_input});")
            .Line("var source = events.source(org.axonframework.eventsourcing.eventstore.SourcingCondition.conditionFor(org.axonframework.messaging.eventstreaming.EventCriteria.havingAnyTag()));")
            .Block("try", body => body.Line("var actual = source.filter(entry -> !(entry.message() instanceof org.axonframework.eventsourcing.eventstore.TerminalEventMessage)).collect(java.util.ArrayList<Object>::new, (items, event) -> items.add(event.payload())).get(5, java.util.concurrent.TimeUnit.SECONDS);")
                .Line($"org.assertj.core.api.Assertions.assertThat(actual).{(specification.ThenEventsInAnyOrder ? "containsExactlyInAnyOrder" : "containsExactly")}({string.Join(", ", _expected)});"))
            .Block("finally", body => body.Line("source.close();"));
    }

    bool FixturesSupported() => NoPriorState() && OnlyEventAssertions() && NoRejection() && NoGeneration() && (specification.ThenNoEvents || !specification.ThenEvents.IsEmpty);

    bool NoPriorState() => specification.GivenEvents.IsEmpty && specification.GivenReadModels.IsEmpty && specification.GivenCaptures.IsEmpty;

    bool OnlyEventAssertions() => specification.ThenReadModels.IsEmpty && specification.ThenAbsentReadModels.IsEmpty && specification.ThenQueries.IsEmpty;

    bool NoRejection() => specification.ThenErrors.IsEmpty && !specification.ThenDenied && specification.ThenReturns is null;

    bool NoGeneration() => specification.GivenGenerated.IsEmpty && specification.WhenClock is null;

    bool TargetSupported()
    {
        var triggers = ScopeSelection.Locate(context.Request.Model.Application).SelectMany(slice => slice.Slice.Reactions).SelectMany(reaction => reaction.Triggers).ToArray();
        if (triggers.Any(trigger => trigger.Kind != SemanticReactionTriggerKind.ApplicationTrigger)) return false;
        return triggers.Count(trigger => trigger.Source == specification.WhenTrigger!.Trigger) == 1;
    }
}
