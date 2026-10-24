// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Cratis.Stage.Rendering.Axon.Emitters.Specifications;

internal sealed record AxonHttpRequest(string Method, string Path, [StringSyntax(StringSyntaxAttribute.Json)] string? JsonBody = null);
