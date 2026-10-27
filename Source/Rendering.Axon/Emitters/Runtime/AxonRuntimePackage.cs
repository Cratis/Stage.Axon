// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Emitters.Runtime;

/// <summary>
/// The one place the renderer names the generated Axon runtime helper package, so that moving the helpers into a published
/// runtime artifact changes this seam instead of every emitter.
/// </summary>
internal static class AxonRuntimePackage
{
    internal const string GroupId = "io.cratis";
    internal const string ArtifactId = "stage-axon-runtime";

    /// <summary>The runtime artifact version a later slice pins in the generated pom. Unused while the helpers are still generated.</summary>
    internal const string Version = "0.1.0";

    const string Segment = ".runtime";

    /// <summary>The Java package holding the runtime helpers that move into the runtime artifact.</summary>
    internal static string JavaPackage(AxonEmissionContext context) => JavaPackage(context.BasePackage);

    /// <summary>The Java package holding the runtime helpers that move into the runtime artifact.</summary>
    internal static string JavaPackage(string basePackage) => basePackage + Segment;

    /// <summary>The fully qualified name of a runtime helper, optionally below a sub-package such as <c>persistent.Name</c>.</summary>
    internal static string Type(AxonEmissionContext context, string name) => Type(context.BasePackage, name);

    /// <summary>The fully qualified name of a runtime helper, optionally below a sub-package such as <c>persistent.Name</c>.</summary>
    internal static string Type(string basePackage, string name) => JavaPackage(basePackage) + "." + name;

    /// <summary>The package of runtime types that stay generated inline because they depend on the model.</summary>
    internal static string InlinePackage(AxonEmissionContext context) => InlinePackage(context.BasePackage);

    /// <summary>The package of runtime types that stay generated inline because they depend on the model.</summary>
    internal static string InlinePackage(string basePackage) => basePackage + Segment;
}
