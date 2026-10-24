// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Conformance;

/// <summary>Accounts for a modeled specification without claiming disabled tests passed.</summary>
/// <param name="SemanticId">The specification's opaque semantic identity.</param>
/// <param name="DisabledCode">The registered disabling diagnostic, or null for an enabled test.</param>
public record SpecificationManifestEntry(string SemanticId, string? DisabledCode);
