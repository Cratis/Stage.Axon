// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;

namespace Cratis.Stage.Rendering.Axon.Admission;

/// <summary>Reports application-level Screenplay constructs no Axon emitter reads.</summary>
internal static class ApplicationUnsupportedSemantics
{
    internal static IEnumerable<AxonBehaviorDecision> Decisions(SemanticApplication application, string package)
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

        if (!application.Audiences.IsDefaultOrEmpty) yield return AxonUnsupportedSemantics.Decision(package, application.Id, $"{application.Id}/unsupported/audiences", "audiences");
        if (!application.Availabilities.IsDefaultOrEmpty) yield return AxonUnsupportedSemantics.Decision(package, application.Id, $"{application.Id}/unsupported/availabilities", "availabilities");
        if (!application.Provides.IsDefaultOrEmpty) yield return AxonUnsupportedSemantics.Decision(package, application.Id, $"{application.Id}/unsupported/provides", "provided context values");
        if (!application.Screens.IsDefaultOrEmpty) yield return AxonUnsupportedSemantics.Decision(package, application.Id, $"{application.Id}/unsupported/screens", "screens");
        if (application.Agents is not null) yield return AxonUnsupportedSemantics.Decision(package, application.Id, $"{application.Id}/unsupported/agents", "agents");
        if (application.TimeZone is { } zone) yield return AxonUnsupportedSemantics.Decision(package, application.Id, $"{application.Id}/unsupported/time zone", $"application time zone {zone.Id}");
        if (!application.ContextMembers.IsDefaultOrEmpty) yield return AxonUnsupportedSemantics.Decision(package, application.Id, $"{application.Id}/unsupported/context members", "application context members");

        foreach (var (label, items) in groups)
        {
            foreach (var item in AxonBehaviorAdmission.Items(items))
            {
                var name = item?.GetType().GetProperty("Name")?.GetValue(item) as string ?? string.Empty;
                yield return AxonUnsupportedSemantics.Decision(package, application.Id, $"{application.Id}/unsupported/{label}/{name}", $"{label} {name}".TrimEnd());
            }
        }
    }
}
