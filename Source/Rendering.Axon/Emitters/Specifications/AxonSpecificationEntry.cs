// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;

namespace Cratis.Stage.Rendering.Axon.Emitters.Specifications;

internal sealed record AxonSpecificationEntry(SemanticId Id, string TestClass, string Method, string? DisabledCode = null);
