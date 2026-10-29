// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Scaffold;

internal static class PersistentProfile
{
    internal const string Name = "persistent";

    internal static bool Offered(AxonEmissionContext context) =>
        !ScopeSelection.Locate(context.Request.Model.Application).Any(located => located.Slice.Constraints.Any());
}
