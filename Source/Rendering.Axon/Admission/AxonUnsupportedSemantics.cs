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
        Decisions(request.Model.Application, ScopeSelection.Select(request), basePackage, located => AvailabilityGates.Gated(request, located));

    internal static IEnumerable<AxonBehaviorDecision> Decisions(SemanticApplication application, IEnumerable<LocatedSemanticSlice> selected, string basePackage) =>
        Decisions(application, selected, basePackage, AvailabilityGates.Gated);

    internal static AxonBehaviorDecision Decision(string package, SemanticId owner, string modelPath, string construct) =>
        new(AxonRefusalCodes.UnsupportedSemantics, package, owner, modelPath, $"render the {construct}", $"The {construct} is not rendered; Axon has no support for this Screenplay 6 construct.");

    static IEnumerable<AxonBehaviorDecision> Decisions(SemanticApplication application, IEnumerable<LocatedSemanticSlice> selected, string basePackage, Func<LocatedSemanticSlice, IEnumerable<SemanticId>> gatedOperations)
    {
        foreach (var decision in ApplicationUnsupportedSemantics.Decisions(application, basePackage)) yield return decision;

        var reported = new HashSet<SemanticId>();
        foreach (var located in selected)
        {
            var package = basePackage + "." + Java.JavaNames.PackageSegment(located.Module.Name);
            foreach (var query in located.Slice.Queries.Where(query => query.Narrowing is not null)) yield return Narrowing(package, query);
            foreach (var gated in gatedOperations(located)) yield return AvailabilityGate(package, gated);
            foreach (var decision in ScopeProvides(located, package, reported)) yield return decision;
            foreach (var label in SliceUnsupportedSemantics.Labels(located.Slice, []).Distinct(StringComparer.Ordinal))
            {
                yield return Decision(package, located.Slice.Id, $"{located.Slice.Id}/unsupported/{label}", label);
            }
        }
    }

    static IEnumerable<AxonBehaviorDecision> ScopeProvides(LocatedSemanticSlice located, string package, HashSet<SemanticId> reported)
    {
        if (!located.Module.Provides.IsDefaultOrEmpty && reported.Add(located.Module.Id)) yield return Decision(package, located.Module.Id, $"{located.Module.Id}/unsupported/provides", "provided context values");
        foreach (var feature in located.Features.Where(feature => !feature.Provides.IsDefaultOrEmpty && reported.Add(feature.Id)))
        {
            yield return Decision(package, feature.Id, $"{feature.Id}/unsupported/provides", "provided context values");
        }
    }

    static AxonBehaviorDecision Narrowing(string package, SemanticKeyedQuery query) =>
        new("STAGE-AXON-AUTHORIZATION", package, query.Id, $"{query.Id}/authorization/narrowing", "authorize query row narrowing", "Policy row narrowing is not rendered; access is denied.");

    static AxonBehaviorDecision AvailabilityGate(string package, SemanticId owner) =>
        new("STAGE-AXON-AUTHORIZATION", package, owner, $"{owner}/authorization/availability", "authorize availability-gated access", "Availability gates are not rendered; access is denied.");
}
