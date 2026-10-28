// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Emitters.Automations;

internal sealed record PreparedReactionTrigger(string Annotation, ReactionInput Input, string Invocation, string? Condition)
{
    internal ReactionProduction? Production { get; init; }

    internal string? NativeBody { get; init; }

    internal bool UsesClock => NativeBody is not null || Invocation.Contains("clock.", StringComparison.Ordinal) || (Condition?.Contains("clock.", StringComparison.Ordinal) ?? false);
}
