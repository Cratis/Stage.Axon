// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace Cratis.Stage.Rendering.Axon;

/// <summary>
/// Defines typed Axon rendering options. Defaults preserve the existing artifact bytes.
/// </summary>
public sealed record AxonRenderOptions
{
    /// <summary>
    /// Gets the Maven artifactId override, used verbatim. Null keeps the base package plus "-application".
    /// </summary>
    public string? ProjectName { get; init; }

    /// <summary>
    /// Gets the Java base package and Maven groupId override, normalized per segment. Null preserves the model-derived package.
    /// Each segment must contain an ASCII letter or digit before normalization. Normalized segments must start with a lowercase
    /// ASCII letter, contain only lowercase ASCII letters, digits or underscores, and remain canonical under normalization.
    /// Framework package roots are refused with STAGE-AXON-NAMES, as are segments that fail these rules.
    /// </summary>
    public string? JavaPackage { get; init; }

    /// <summary>
    /// Gets plain-text lines to prefix Java files as comments. Empty preserves existing bytes.
    /// </summary>
    public ImmutableArray<string> FileHeader { get; init; } = [];

    /// <summary>
    /// Gets whether unsupported constructs produce errors and refusals. False preserves the default warning behavior.
    /// </summary>
    public bool Strict { get; init; }

    /// <summary>
    /// Gets approximation codes explicitly allowed. Approximations always render with a warning; listing a code also keeps strict mode from refusing it.
    /// </summary>
    public ImmutableArray<string> AllowedApproximations { get; init; } = [];

    /// <summary>
    /// Gets whether to generate the TypeScript client. False preserves output without a client.
    /// </summary>
    public bool TypeScriptClient { get; init; }

    /// <summary>
    /// Gets the HTTP authentication mode. HTTP Basic is the default.
    /// </summary>
    public AxonHttpAuthentication HttpAuthentication { get; init; } = AxonHttpAuthentication.Basic;
}
