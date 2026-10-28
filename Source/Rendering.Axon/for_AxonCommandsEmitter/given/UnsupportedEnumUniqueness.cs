// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter.given;

internal static class UnsupportedEnumUniqueness
{
    internal static SemanticConstraint For(SemanticEventContract contract) =>
        new("CustomerUnique", SemanticConstraintKind.UniquePropertyValue, SemanticConstraintScope.EventSequence, [new(contract.Id, [contract.Properties.Single(property => property.Name == "status").Id])], [], false, null);
}
