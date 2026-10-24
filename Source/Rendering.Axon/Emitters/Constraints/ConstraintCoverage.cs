// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Constraints;

internal static class ConstraintCoverage
{
    internal static bool CommandRequiresUnsupportedConstraint(AxonEmissionContext context, SemanticCommand command) =>
        command.Produces.Any(produced => EventRequiresUnsupportedConstraint(context, produced.EventContract));

    internal static bool EventRequiresUnsupportedConstraint(AxonEmissionContext context, SemanticId contract) =>
        ScopeSelection.Locate(context.Request.Model.Application).SelectMany(slice => slice.Slice.Constraints)
            .Any(constraint => constraint.Targets.Any(target => target.EventContract == contract) || constraint.ReleasedBy.Contains(contract));
}
