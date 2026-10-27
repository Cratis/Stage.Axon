// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace Cratis.Stage.Rendering.Axon.Admission;

internal static partial class AxonRefusalCodes
{
    internal const string TranslationCapture = "STAGE-AXON-0601";
    internal const string RefusalBranch = "STAGE-AXON-0506";
    internal const string ConceptCompliance = "STAGE-AXON-COMPLIANCE";
    internal const string Schedule = "STAGE-AXON-0507";

    internal static readonly ImmutableHashSet<string> Approximations = [Schedule];
}
