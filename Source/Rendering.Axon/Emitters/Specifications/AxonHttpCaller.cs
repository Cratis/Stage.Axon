// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace Cratis.Stage.Rendering.Axon.Emitters.Specifications;

internal sealed record AxonHttpCaller(string Identity, ImmutableArray<string> Roles, bool Authenticated);
