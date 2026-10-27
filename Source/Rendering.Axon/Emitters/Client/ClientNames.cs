// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Client;

/// <summary>Names generated TypeScript identifiers and object keys.</summary>
internal static partial class ClientNames
{
    static readonly HashSet<string> _reserved = new(
        ("break case catch class const continue debugger default delete do else enum export extends false finally for function if import in instanceof new null return super switch this throw true try typeof var void while with yield let static implements interface package private protected public await async " +
         "any as asserts bigint boolean constructor declare get infer is keyof module namespace never number object of readonly require satisfies set string symbol type undefined unique unknown " +
         "Array Boolean Date Error Function Map Number Object Promise Record Set String Symbol Partial Readonly Uint8Array ReadableStream AbortSignal AbortController Response JSON Math globalThis").Split(' '),
        StringComparer.Ordinal);

    [GeneratedRegex("^[A-Za-z_$][A-Za-z0-9_$]*$", RegexOptions.None, 1000)]
    private static partial Regex Identifier { get; }

    /// <summary>Gets a type name for a modeled name.</summary>
    /// <param name="name">The modeled name.</param>
    /// <returns>A PascalCase identifier that does not shadow a keyword or a global.</returns>
    internal static string Type(string name) => Protect(JavaNames.Type(name));

    /// <summary>Gets a value name for a modeled name.</summary>
    /// <param name="name">The modeled name.</param>
    /// <returns>A camelCase identifier that does not shadow a keyword or a global.</returns>
    internal static string Value(string name) => Protect(JavaNames.Member(name));

    /// <summary>Gets a directory or file segment for a modeled name.</summary>
    /// <param name="name">The modeled name.</param>
    /// <returns>The lower-case segment shared with the Java package path.</returns>
    internal static string Segment(string name) => JavaNames.PackageSegment(name);

    /// <summary>Renders an interface member name.</summary>
    /// <param name="key">The wire key.</param>
    /// <returns>The key as an identifier, or a quoted string when it is not one.</returns>
    internal static string Member(string key) => Identifier.IsMatch(key) ? key : Literal(key);

    /// <summary>Renders a string literal.</summary>
    /// <param name="text">The text.</param>
    /// <returns>A double-quoted TypeScript string literal.</returns>
    internal static string Literal(string text) => System.Text.Json.JsonSerializer.Serialize(text, System.Text.Json.JsonSerializerOptions.Default).Replace("\u2028", "\\u2028", StringComparison.Ordinal).Replace("\u2029", "\\u2029", StringComparison.Ordinal);

    /// <summary>Strips characters that could end a comment.</summary>
    /// <param name="text">The comment text.</param>
    /// <returns>A single line that is safe inside a block or line comment.</returns>
    internal static string Comment(string text) => text.Replace("*/", "* /", StringComparison.Ordinal).ReplaceLineEndings(" ");

    static string Protect(string name) => _reserved.Contains(name) ? name + "_" : name;
}
