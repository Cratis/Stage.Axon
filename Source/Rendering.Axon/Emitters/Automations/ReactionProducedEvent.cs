// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Emitters.Automations;

internal sealed record ReactionProducedEvent(string Type, string Expression, string Source, string? Condition);
