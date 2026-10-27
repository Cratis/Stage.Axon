// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Admission;

internal static partial class AxonRefusalCodes
{
    /// <summary>A contract shape the generated TypeScript client cannot type; reported through unsupported-construct degradation.</summary>
    internal const string Client = "STAGE-AXON-CLIENT";

    /// <summary>A number the JSON wire cannot carry exactly in a TypeScript number; always a warning.</summary>
    internal const string ClientPrecision = "STAGE-AXON-CLIENT-PRECISION";
}
