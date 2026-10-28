// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Cratis.Stage.Rendering.Axon.Java;

internal sealed partial class JavaCodeBuilder(string package)
{
    const string LocalType = "";
    static readonly string[] _frameworkPrefixes =
    [
        "java.", "javax.", "jakarta.", "org.axonframework.", "org.springframework.",
        "org.reactivestreams.", "org.junit.", "org.mockito.", "org.assertj.", "io.cratis.", "io.axoniq."
    ];

    readonly SortedSet<string> _imports = new(StringComparer.Ordinal);
    readonly Dictionary<string, string> _simpleNames = new(StringComparer.Ordinal);
    readonly StringBuilder _body = new();
    int _indent;

    [GeneratedRegex("\"(?:\\\\.|[^\"\\\\])*\"|(?<![A-Za-z0-9_$.])(?:[a-z][a-z0-9_]*\\.)+[A-Z][A-Za-z0-9_]*", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex ExpressionTypes { get; }

    internal static string StringLiteral(string value)
    {
        var escaped = new StringBuilder("\"");
        foreach (var character in value)
        {
            escaped.Append(character switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                '\b' => "\\b",
                '\f' => "\\f",
                _ when char.IsControl(character) => "\\u" + ((int)character).ToString("x4", CultureInfo.InvariantCulture),
                _ => character.ToString()
            });
        }

        return escaped.Append('"').ToString();
    }

    internal static string UnsupportedComment(string intent, string modelPath, string code) =>
        $"// Screenplay wanted to {CommentText(intent)} ({CommentText(modelPath)}), but this is not supported by the Axon target yet ({CommentText(code)}).";

    internal static bool IsFramework(string type) => _frameworkPrefixes.Any(prefix => type.StartsWith(prefix, StringComparison.Ordinal));

    /// <summary>
    /// Imports a type and claims its simple name. Use it for generated application types from another package, before emitting
    /// code that calls <see cref="Reference"/>, so application types keep their short names.
    /// </summary>
    internal JavaCodeBuilder Import(string type)
    {
        Reference(type);
        return this;
    }

    /// <summary>
    /// Claims a simple name for a generated application type that the file declares or that lives in its own package, so
    /// <see cref="Reference"/> qualifies any framework type with the same name. Declare names before emitting code that uses them.
    /// </summary>
    internal JavaCodeBuilder Declare(string simpleName)
    {
        if (_simpleNames.TryGetValue(simpleName, out var owner)) _imports.Remove(owner);
        _simpleNames[simpleName] = LocalType;
        return this;
    }

    /// <summary>
    /// Returns the name to emit for a framework type: the simple name with an import, or the qualified name when an application
    /// type or an earlier referenced type already holds the simple name. The first claim keeps a name, so emission order decides
    /// collisions deterministically. <c>java.lang</c> types need no import.
    /// </summary>
    internal string Reference(string qualifiedName)
    {
        var simpleName = SimpleName(qualifiedName);
        if (simpleName.Length == qualifiedName.Length) return qualifiedName;
        if (_simpleNames.TryGetValue(simpleName, out var owner) && owner != qualifiedName) return qualifiedName;

        _simpleNames[simpleName] = qualifiedName;
        if (!IsJavaLang(qualifiedName)) _imports.Add(qualifiedName);
        return simpleName;
    }

    internal JavaCodeBuilder Line(string text = "")
    {
        if (text.Length > 0) _body.Append(' ', _indent * 4).Append(text);
        _body.Append('\n');
        return this;
    }

    internal JavaCodeBuilder UnsupportedIntent(string intent, string modelPath, string code) =>
        Line(UnsupportedComment(intent, modelPath, code));

    internal JavaCodeBuilder DiffersFromScreenplay(string difference) =>
        Line($"// Differs from Screenplay: {CommentText(difference)}");

    internal JavaCodeBuilder RawBody(string body)
    {
        _body.Append(body);
        if (!body.EndsWith('\n')) _body.Append('\n');
        return this;
    }

    /// <summary>Reserves modeled types in renderer-produced expressions before framework references.</summary>
    internal void ClaimExpressions(IEnumerable<string> expressions)
    {
        foreach (var expression in expressions)
        {
            foreach (Match match in ExpressionTypes.Matches(expression))
            {
                if (match.Value.StartsWith('"') || IsFramework(match.Value)) continue;
                _simpleNames.TryAdd(SimpleName(match.Value), match.Value);
            }
        }
    }

    /// <summary>Resolves renderer-produced expressions, never authored Java bodies; string literals stay verbatim.</summary>
    internal string ReferenceExpression(string expression) => ExpressionTypes.Replace(expression, match =>
        match.Value.StartsWith('"') ? match.Value : Reference(match.Value));

    internal JavaCodeBuilder Block(string declaration, Action<JavaCodeBuilder> body)
    {
        Line(declaration + " {");
        _indent++;
        body(this);
        _indent--;
        return Line("}");
    }

    internal string Build()
    {
        var result = new StringBuilder();
        result.Append("package ").Append(package).Append(";\n\n");
        foreach (var type in _imports) result.Append("import ").Append(type).Append(";\n");
        if (_imports.Count > 0) result.Append('\n');
        return result.Append(_body).ToString();
    }

    static string SimpleName(string qualifiedName) => qualifiedName[(qualifiedName.LastIndexOf('.') + 1)..];

    static bool IsJavaLang(string qualifiedName) =>
        qualifiedName.StartsWith("java.lang.", StringComparison.Ordinal) && qualifiedName.IndexOf('.', "java.lang.".Length) < 0;

    static string CommentText(string text) => text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace('\r', ' ').Replace('\n', ' ');
}
