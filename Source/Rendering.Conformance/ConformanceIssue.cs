// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Conformance;

/// <summary>Describes a renderer conformance violation.</summary>
/// <param name="Code">The stable conformance code.</param>
/// <param name="Message">The violation details.</param>
/// <param name="SemanticId">The subject's opaque semantic identity, when available.</param>
public record ConformanceIssue(string Code, string Message, string? SemanticId = null);
