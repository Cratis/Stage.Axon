// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Emitters;

internal static class AxonEmitters
{
    internal static IReadOnlyList<IAxonEmitter> Ordered { get; } =
    [
        new Scaffold.AxonScaffoldEmitter(),
        new Concepts.AxonConceptsEmitter(),
        new Events.AxonEventsEmitter(),
        new Commands.AxonCommandsEmitter(),
        new Constraints.AxonConstraintsEmitter(),
        new Decisions.AxonDecisionsEmitter(),
        new Projections.AxonProjectionsEmitter(),
        new Queries.AxonQueriesEmitter(),
        new Automations.AxonAutomationsEmitter(),
        new Persistence.AxonPersistenceEmitter(),
        new Security.AxonSecurityEmitter(),
        new Specifications.AxonSpecificationsEmitter(),
        new Runtime.AxonRuntimeEmitter()
    ];
}
