// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence;

internal static partial class PersistentJavaTemplate
{
    static readonly string[] _javaLangTypes =
    [
        "Object", "String", "Integer", "Long", "Boolean", "Void", "Exception", "Throwable", "RuntimeException",
        "Class", "System", "Math", "Thread", "IllegalStateException", "IllegalArgumentException", "AssertionError",
        "Number", "StringBuilder", "AutoCloseable", "Override", "SuppressWarnings"
    ];

    [GeneratedRegex(@"(?m)^[ \t]*(?:(?:public|private|protected|static|abstract|final)\s+)*(?:class|interface|enum|record)\s+(?<type>[A-Za-z_$][\w$]*)", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex Declarations { get; }

    internal static string Render(string source)
    {
        var lines = source.Split('\n');
        var package = lines.Single(line => line.StartsWith("package ", StringComparison.Ordinal))[8..].TrimEnd(';', '\r');
        var code = new JavaCodeBuilder(package);
        var body = string.Join('\n', lines.Where(line => !Header(line))).TrimStart('\n', '\r');
        Claim(code, body, lines);
        var staticImports = lines.Where(line => line.StartsWith("import static ", StringComparison.Ordinal)).ToArray();
        if (staticImports.Length > 0) code.RawBody(string.Join('\n', staticImports) + "\n\n");
        return code.RawBody(code.ReferenceExpression(body)).Build();
    }

    static void Claim(JavaCodeBuilder code, string body, IEnumerable<string> lines)
    {
        foreach (Match declaration in Declarations.Matches(body)) code.Declare(declaration.Groups["type"].Value);
        foreach (var type in _javaLangTypes) code.Reference("java.lang." + type);
        foreach (var line in lines.Where(line => line.StartsWith("import ", StringComparison.Ordinal) && !line.StartsWith("import static ", StringComparison.Ordinal)))
        {
            code.Import(line[7..].TrimEnd(';', '\r'));
        }
    }

    static bool Header(string line) => line.StartsWith("package ", StringComparison.Ordinal) || line.StartsWith("import ", StringComparison.Ordinal);
}
