// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Conformance;

/// <summary>One native test result correlated to a modeled specification.</summary>
/// <param name="SemanticId">The specification's opaque semantic identity.</param>
/// <param name="Passed">Whether execution passed; skipped tests are not passes.</param>
public record SpecificationResult(string SemanticId, bool Passed);
