// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Cratis.Stage.Rendering.Axon.Emitters.Specifications;

internal sealed record AxonHttpResponse(int Status, [StringSyntax(StringSyntaxAttribute.Json)] string? ExpectedJson = null)
{
    internal ImmutableArray<string> Assertions { get; init; } = [];
}
