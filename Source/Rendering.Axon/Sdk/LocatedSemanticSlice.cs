// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;

namespace Cratis.Stage.Rendering.Axon.Sdk;

internal sealed record LocatedSemanticSlice(
    SemanticModule Module,
    IReadOnlyList<SemanticFeature> Features,
    SemanticSlice Slice,
    IReadOnlyList<string> Path)
{
    /// <summary>Gets a value indicating whether the module, a containing feature or the slice declares an availability gate.</summary>
    internal bool HasAvailabilityGate => !Module.Available.IsDefaultOrEmpty || Features.Any(feature => !feature.Available.IsDefaultOrEmpty) || !Slice.Available.IsDefaultOrEmpty;
}
