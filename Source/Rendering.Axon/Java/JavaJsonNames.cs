// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Java;

internal static class JavaJsonNames
{
    /// <summary>
    /// Returns the <c>@JsonProperty</c> annotation, with a trailing space, that keeps the modelled wire name when the Java member
    /// name differs from it, and an empty string when they match.
    /// </summary>
    /// <param name="modelName">The modelled member name.</param>
    /// <param name="code">The builder that receives the annotation import.</param>
    /// <returns>The annotation text, or an empty string.</returns>
    internal static string Annotation(string modelName, JavaCodeBuilder code) => JavaNames.Member(modelName) == modelName
        ? string.Empty
        : $"@{code.Reference("com.fasterxml.jackson.annotation.JsonProperty")}({JavaCodeBuilder.StringLiteral(modelName)}) ";
}
