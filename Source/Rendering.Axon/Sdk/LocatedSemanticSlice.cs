// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;

namespace Cratis.Stage.Rendering.Axon.Sdk;

internal sealed record LocatedSemanticSlice(
    SemanticModule Module,
    IReadOnlyList<SemanticFeature> Features,
    SemanticSlice Slice,
    IReadOnlyList<string> Path);
