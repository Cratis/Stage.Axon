// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Sdk;

internal enum SemanticSurfaceDispositionKind
{
    Rendered,
    Rejected,
    Ignored
}

internal sealed record SemanticSurfaceDisposition(SemanticSurfaceDispositionKind Kind, string Detail);
