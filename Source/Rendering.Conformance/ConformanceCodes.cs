// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Conformance;

/// <summary>Stable conformance codes. Codes are never reused or renumbered.</summary>
public static class ConformanceCodes
{
    /// <summary>A reachable identity has no artifact, refusal or diagnostic.</summary>
    public const string UnaccountedIdentity = "STAGE-CONF-001";

    /// <summary>An identity is both realized and refused.</summary>
    public const string RealizedAndRefused = "STAGE-CONF-002";

    /// <summary>A diagnostic or disabled specification uses an unregistered code.</summary>
    public const string UnregisteredCode = "STAGE-CONF-003";

    /// <summary>Repeated rendering produces different plans.</summary>
    public const string NondeterministicOutput = "STAGE-CONF-004";

    /// <summary>A modeled specification is missing from the manifest or enabled results.</summary>
    public const string MissingSpecification = "STAGE-CONF-005";

    /// <summary>A specification appears more than once in the manifest or results.</summary>
    public const string DuplicateSpecification = "STAGE-CONF-006";

    /// <summary>An enabled specification failed, errored or was skipped.</summary>
    public const string FailedSpecification = "STAGE-CONF-007";

    /// <summary>A manifest or result is malformed or names an unknown specification.</summary>
    public const string MalformedManifest = "STAGE-CONF-008";
}
