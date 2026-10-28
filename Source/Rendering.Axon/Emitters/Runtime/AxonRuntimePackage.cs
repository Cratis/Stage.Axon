// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Emitters.Runtime;

/// <summary>
/// The one place the renderer names the Axon runtime helper packages: the published <c>io.cratis:stage-axon-runtime</c> module for
/// the model-independent helpers and the generated inline package for the helpers that still depend on the model.
/// </summary>
internal static class AxonRuntimePackage
{
    internal const string GroupId = "io.cratis";
    internal const string ArtifactId = "stage-axon-runtime";

    /// <summary>The Java package of the helpers published in the runtime artifact.</summary>
    internal const string ModulePackage = "io.cratis.stage.axon.runtime";

    /// <summary>The names of the helpers that live in the runtime artifact instead of being generated into each application.</summary>
    internal static readonly IReadOnlySet<string> ModuleTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "Caller",
        "CommandValidationException",
        "EventSourceId",
        "InMemoryReadModelStore",
        "InMemoryUniqueConfiguration",
        "InMemoryUniqueValueStore",
        "LiveCapacityException",
        "LiveSnapshots",
        "LiveStreams",
        "persistent.JpaUniqueValueStore",
        "persistent.PersistentConfiguration",
        "persistent.PersistentReadModelSafety",
        "persistent.UniqueClaim",
        "ProblemDetailsAdvice",
        "QueryCardinalityException",
        "QueryResults",
        "ReadModelStore",
        "ReadModelTransaction",
        "SecuritySupport",
        "SseBridge",
        "UniqueValueConflict",
        "UniqueValueStore",
        "UniqueValues",
        "ValidationContext",
        "ValidationError",
        "views.ViewRow",
        "views.ViewGeneration",
        "views.JpaReadModelStore",
        "views.PersistentViewBootstrap",
        "views.PersistentViewProcessorGuard",
    };

    const string Segment = ".runtime";

    /// <summary>The runtime artifact version the generated pom pins: the renderer's own version, because both are published by one release.</summary>
    internal static string Version => AxonRendering.Version;

    /// <summary>The Java package of the runtime helpers that are still generated into the application.</summary>
    internal static string JavaPackage(AxonEmissionContext context) => JavaPackage(context.BasePackage);

    /// <summary>The Java package of the runtime helpers that are still generated into the application.</summary>
    internal static string JavaPackage(string basePackage) => basePackage + Segment;

    /// <summary>The fully qualified name of a runtime helper, optionally below a sub-package such as <c>persistent.Name</c>.</summary>
    internal static string Type(AxonEmissionContext context, string name) => Type(context.BasePackage, name);

    /// <summary>The fully qualified name of a runtime helper, optionally below a sub-package such as <c>persistent.Name</c>.</summary>
    internal static string Type(string basePackage, string name) => ModuleTypes.Contains(name) ? ModulePackage + "." + name : JavaPackage(basePackage) + "." + name;

    /// <summary>Whether a Java type belongs to the runtime artifact or to the generated runtime package.</summary>
    internal static bool IsRuntimeType(string basePackage, string javaType) =>
        javaType.StartsWith(JavaPackage(basePackage) + ".", StringComparison.Ordinal) || javaType.StartsWith(ModulePackage + ".", StringComparison.Ordinal);

    /// <summary>The package of runtime types that stay generated inline because they depend on the model.</summary>
    internal static string InlinePackage(AxonEmissionContext context) => InlinePackage(context.BasePackage);

    /// <summary>The package of runtime types that stay generated inline because they depend on the model.</summary>
    internal static string InlinePackage(string basePackage) => basePackage + Segment;
}
