// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Admission;

/// <summary>Reports Screenplay 6 constructs no Axon emitter reads (state machines, workflows, graph, clock and calendar operands, one-of expressions, possible events, time zones, screens, agents, systems/abilities, projection ordering/set-once, caller identity and text comparisons), plus policy row narrowing, so none is dropped silently.</summary>
internal static class AxonUnsupportedSemantics
{
    internal static IEnumerable<AxonBehaviorDecision> Decisions(ArtifactRenderRequest request, string basePackage) =>
        Decisions(request.Model.Application, ScopeSelection.Select(request), basePackage);

    internal static IEnumerable<AxonBehaviorDecision> Decisions(SemanticApplication application, IEnumerable<LocatedSemanticSlice> selected, string basePackage)
    {
        foreach (var decision in ApplicationDecisions(application, basePackage)) yield return decision;

        var reported = new HashSet<SemanticId>();
        foreach (var located in selected)
        {
            var package = basePackage + "." + Java.JavaNames.PackageSegment(located.Module.Name);
            foreach (var query in located.Slice.Queries.Where(query => query.Narrowing is not null)) yield return Narrowing(package, query);
            foreach (var gated in AvailabilityGated(located)) yield return AvailabilityGate(package, gated);
            foreach (var decision in ScopeProvides(located, package, reported)) yield return decision;
            foreach (var label in SliceConstructs(located.Slice, []).Distinct(StringComparer.Ordinal))
            {
                yield return Decision(package, located.Slice.Id, $"{located.Slice.Id}/unsupported/{label}", label);
            }
        }
    }

    static IEnumerable<SemanticId> AvailabilityGated(LocatedSemanticSlice located) =>
        located.Slice.Commands.Where(command => located.HasAvailabilityGate || !command.Available.IsDefaultOrEmpty).Select(command => command.Id)
            .Concat(located.Slice.Queries.Where(query => located.HasAvailabilityGate || !query.Available.IsDefaultOrEmpty).Select(query => query.Id));

    static IEnumerable<AxonBehaviorDecision> ScopeProvides(LocatedSemanticSlice located, string package, HashSet<SemanticId> reported)
    {
        if (!located.Module.Provides.IsDefaultOrEmpty && reported.Add(located.Module.Id)) yield return Decision(package, located.Module.Id, $"{located.Module.Id}/unsupported/provides", "provided context values");
        foreach (var feature in located.Features.Where(feature => !feature.Provides.IsDefaultOrEmpty && reported.Add(feature.Id)))
        {
            yield return Decision(package, feature.Id, $"{feature.Id}/unsupported/provides", "provided context values");
        }
    }

    static IEnumerable<AxonBehaviorDecision> ApplicationDecisions(SemanticApplication application, string package)
    {
        var groups = new (string Label, System.Collections.IEnumerable Items)[]
        {
            ("state machine", application.StateMachines),
            ("workflow", application.Workflows),
            ("graph", application.Graphs),
            ("graph projection", application.GraphProjections),
            ("graph specification", application.GraphSpecifications),
            ("graph query", application.GraphQueries),
            ("systems", application.Systems)
        };

        if (!application.Audiences.IsDefaultOrEmpty) yield return Decision(package, application.Id, $"{application.Id}/unsupported/audiences", "audiences");
        if (!application.Availabilities.IsDefaultOrEmpty) yield return Decision(package, application.Id, $"{application.Id}/unsupported/availabilities", "availabilities");
        if (!application.Provides.IsDefaultOrEmpty) yield return Decision(package, application.Id, $"{application.Id}/unsupported/provides", "provided context values");
        if (!application.Screens.IsDefaultOrEmpty) yield return Decision(package, application.Id, $"{application.Id}/unsupported/screens", "screens");
        if (application.Agents is not null) yield return Decision(package, application.Id, $"{application.Id}/unsupported/agents", "agents");
        if (application.TimeZone is { } zone) yield return Decision(package, application.Id, $"{application.Id}/unsupported/time zone", $"application time zone {zone.Id}");
        if (!application.ContextMembers.IsDefaultOrEmpty) yield return Decision(package, application.Id, $"{application.Id}/unsupported/context members", "application context members");

        foreach (var (label, items) in groups)
        {
            foreach (var item in AxonBehaviorAdmission.Items(items))
            {
                var name = item?.GetType().GetProperty("Name")?.GetValue(item) as string ?? string.Empty;
                yield return Decision(package, application.Id, $"{application.Id}/unsupported/{label}/{name}", $"{label} {name}".TrimEnd());
            }
        }
    }

    static IEnumerable<string> SliceConstructs(object? node, HashSet<object> seen)
    {
        switch (node)
        {
            case null or string or Enum or SemanticId:
                yield break;
            case { } primitive when primitive.GetType().IsPrimitive:
                yield break;
            case System.Collections.IEnumerable items:
                foreach (var item in AxonBehaviorAdmission.Items(items)) foreach (var found in SliceConstructs(item, seen)) yield return found;
                yield break;
        }

        if (!seen.Add(node)) yield break;
        var own = Construct(node) ?? NewConstruct(node);
        if (own is not null) yield return own;
        foreach (var property in node.GetType().GetProperties().Where(property => property.GetIndexParameters().Length == 0))
        {
            foreach (var found in SliceConstructs(property.GetValue(node), seen)) yield return found;
        }
    }

    static string? Construct(object node) => node switch
    {
        SemanticOneOfExpression => "one-of expression",
        SemanticClockOperand => "clock operand",
        SemanticConditionOperand { DateOf: true } => "date-only operand",
        SemanticCommandCalendar => "command calendar",
        SemanticCommandHandler { PossibleEvents.IsDefaultOrEmpty: false } => "possible events",
        SemanticSpecification { GivenTimeZone.Length: > 0 } => "specification time zone",
        _ => null
    };

    static string? NewConstruct(object node) => node switch
    {
        SemanticSlice { Screens.IsDefaultOrEmpty: false } => "screens",
        SemanticSlice { Provides.IsDefaultOrEmpty: false } => "provided context values",
        SemanticSlice { Adoptions.IsDefaultOrEmpty: false } => "screen adoptions",
        SemanticInvocation { Ability: not null } => "ability invocation",
        SemanticProjectionMapping { Operation: SemanticProjectionOperation.SetOnce } => "set once",
        SemanticProjectionChildren { Ordering: not null } => "children ordering",
        SemanticConditionOperand { CallerId: true } => "caller identity",
        SemanticComparison { Operator: SemanticComparisonOperator.Contains or SemanticComparisonOperator.StartsWith } => "text or membership comparison",
        _ => null
    };

    static AxonBehaviorDecision Narrowing(string package, SemanticKeyedQuery query) =>
        new("STAGE-AXON-AUTHORIZATION", package, query.Id, $"{query.Id}/authorization/narrowing", "authorize query row narrowing", "Policy row narrowing is not rendered; access is denied.");

    static AxonBehaviorDecision AvailabilityGate(string package, SemanticId owner) =>
        new("STAGE-AXON-AUTHORIZATION", package, owner, $"{owner}/authorization/availability", "authorize availability-gated access", "Availability gates are not rendered; access is denied.");

    static AxonBehaviorDecision Decision(string package, SemanticId owner, string modelPath, string construct) =>
        new(AxonRefusalCodes.UnsupportedSemantics, package, owner, modelPath, $"render the {construct}", $"The {construct} is not rendered; Axon has no support for this Screenplay 6 construct.");
}
