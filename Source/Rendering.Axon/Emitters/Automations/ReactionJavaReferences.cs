// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Automations;

/// <summary>Registers types in renderer-produced expressions, without changing string literals.</summary>
internal static partial class ReactionJavaReferences
{
    static readonly string[] _frameworkPrefixes =
    [
        "java.", "javax.", "jakarta.", "org.axonframework.", "org.springframework.",
        "org.reactivestreams.", "org.junit.", "org.mockito.", "org.assertj.", "io.cratis.", "io.axoniq."
    ];

    [GeneratedRegex("\"(?:\\\\.|[^\"\\\\])*\"|(?<![A-Za-z0-9_$.])(?:[a-z][a-z0-9_]*\\.)+[A-Z][A-Za-z0-9_]*", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex Tokens { get; }

    internal static void Claim(JavaCodeBuilder code, IEnumerable<string> expressions)
    {
        foreach (var expression in expressions)
        {
            foreach (Match match in Tokens.Matches(expression))
            {
                if (match.Value.StartsWith('"') || IsFramework(match.Value)) continue;
                code.Reference(match.Value);
            }
        }
    }

    internal static string Expression(JavaCodeBuilder code, string expression) => Tokens.Replace(expression, match =>
        match.Value.StartsWith('"') ? match.Value : code.Reference(match.Value));

    static bool IsFramework(string type) => _frameworkPrefixes.Any(prefix => type.StartsWith(prefix, StringComparison.Ordinal));
}
