// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Cratis.Screenplay.Semantics;

namespace Cratis.Stage.Rendering.Axon.Emitters.Specifications;

internal sealed record AxonHttpScenario(SemanticId Id, ImmutableArray<AxonHttpExchange> Exchanges, AxonHttpCaller Caller)
{
    internal ImmutableArray<string> Arrange { get; init; } = [];
}
