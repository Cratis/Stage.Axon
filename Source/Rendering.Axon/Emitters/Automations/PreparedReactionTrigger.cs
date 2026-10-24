// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Emitters.Automations;

internal sealed record PreparedReactionTrigger(string Annotation, ReactionInput Input, string Invocation, string? Condition);
