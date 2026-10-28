// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Admission;

internal static partial class AxonRefusalCodes
{
    internal const string SupportPackage = "STAGE-AXON-SUPPORT-PACKAGE";

    /// <summary>The renderer was built from source, so generated applications pin an unpublished stage-axon-runtime version; reported as a warning.</summary>
    internal const string LocalRuntime = "STAGE-AXON-LOCAL-RUNTIME";
}
