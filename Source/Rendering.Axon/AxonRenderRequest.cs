// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Cratis.Screenplay.Diagnostics;
using Cratis.Screenplay.Semantics;
using Cratis.Screenplay.Semantics.Execution;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;

namespace Cratis.Stage.Rendering.Axon;

/// <summary>
/// Defines a model-independent Axon planning request with compiler sidecars and typed options.
/// </summary>
/// <param name="Model">The executable semantic model.</param>
/// <param name="Plan">The semantic execution plan.</param>
/// <param name="Scope">The primary selected scope.</param>
public sealed record AxonRenderRequest(ExecutableSemanticModel Model, SemanticExecutionPlan Plan, ArtifactRenderScope Scope)
{
    /// <summary>
    /// Gets additional scopes to union with the primary scope. Application scope cannot be combined.
    /// </summary>
    public ImmutableArray<ArtifactRenderScope> AdditionalScopes { get; init; } = [];

    /// <summary>
    /// Gets compiler implementation requirements whose content revisions are verified before using bodies.
    /// </summary>
    public ImmutableArray<SemanticImplementationRequirement> ImplementationRequirements { get; init; } = [];

    /// <summary>
    /// Gets resolved implementation bodies keyed by stable requirement identity.
    /// </summary>
    public ImmutableDictionary<string, string> ImplementationContents { get; init; } = [];

    /// <summary>
    /// Gets typed contexts from the compilation that produced the model and requirements.
    /// </summary>
    public ImmutableArray<SemanticTypedContextDescriptor> TypedContextDescriptors { get; init; } = [];

    /// <summary>
    /// Gets attachment-loader diagnostics to include with missing-body diagnostics.
    /// </summary>
    public ImmutableArray<Diagnostic> AttachmentDiagnostics { get; init; } = [];

    /// <summary>
    /// Gets the optional whole-application baseline assessment. Not supplied preserves unchecked rendering.
    /// </summary>
    public ReleaseBaselineAssessment ReleaseBaseline { get; init; } = ReleaseBaselineAssessment.NotSupplied;

    /// <summary>
    /// Gets rendering options. Defaults preserve the existing artifact bytes.
    /// </summary>
    public AxonRenderOptions Options { get; init; } = new();
}
