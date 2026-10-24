// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Cratis.Screenplay.Semantics;

namespace Cratis.Stage.Rendering.Axon.Emitters.Decisions;

internal sealed record DecisionPresenceEvents(ImmutableArray<SemanticId> Creates, ImmutableArray<SemanticId> Removes)
{
    internal IEnumerable<SemanticId> All => Creates.Concat(Removes);
}
