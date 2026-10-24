// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Admission;

/// <summary>Stable Axon-owned refusal codes. Reserved construct codes are activated with their emitters.</summary>
internal static class AxonRefusalCodes
{
    internal const string Version = "STAGE-AXON-VERSION";
    internal const string Profile = "STAGE-AXON-PROFILE";
    internal const string Scope = "STAGE-AXON-SCOPE";
    internal const string Scaffold = "STAGE-AXON-SCAFFOLD";
    internal const string GapMode = "STAGE-AXON-GAP-MODE";
    internal const string RefusalMode = "STAGE-AXON-REFUSAL-MODE";
    internal const string Implementation = "STAGE-AXON-IMPL";
    internal const string KeyParts = "STAGE-AXON-KEY-PARTS";
    internal const string CompositeKey = "STAGE-AXON-COMPOSITE-KEY";
    internal const string QueryFilter = "STAGE-AXON-QUERY-FILTER";
    internal const string Generation = "STAGE-AXON-GENERATION";
    internal const string Reduction = "STAGE-AXON-REDUCE";
    internal const string Clock = "STAGE-AXON-CLOCK";
    internal const string Capture = "STAGE-AXON-CAPTURE";
    internal const string Outbound = "STAGE-AXON-OUTBOUND";
    internal const string Evolution = "STAGE-AXON-EVOLUTION";
    internal const string Append = "STAGE-AXON-APPEND";
    internal const string Redelivery = "STAGE-AXON-REDELIVERY";
    internal const string Atomic = "STAGE-AXON-ATOMIC";
    internal const string ProtectedReads = "STAGE-AXON-PROTECTED-READS";
    internal const string Routing = "STAGE-AXON-ROUTING";
}
