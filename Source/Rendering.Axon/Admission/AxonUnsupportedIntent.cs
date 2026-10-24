// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;

namespace Cratis.Stage.Rendering.Axon.Admission;

internal sealed record AxonUnsupportedIntent(string Code, SemanticId Construct, string Intent, string ModelPath);
