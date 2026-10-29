// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security;

internal static class PolicyBodies
{
    static readonly string[] _knownTypes = ["java.util.Map", "org.springframework.security.core.context.SecurityContextHolder"];

    internal static bool NeedsClaims(string body) => !PoliciesConfiguration.Declares(body, "claims") && Match(JavaCodeMask.Blank(body), @"\bclaims\b");

    internal static string? Failure(AxonEmissionContext context, string body)
    {
        var code = JavaCodeMask.Blank(body);
        if (Match(code, @"\bgetDetails\s*\(|\bdetails\b")) return "Legacy details claims have no equivalent native JWT binding.";
        var claimsFailure = NeedsClaims(body) ? ClaimsFailure(context) : null;
        return claimsFailure ?? ImportsFailure(code);
    }

    internal static string Lower(string body, JavaCodeBuilder builder)
    {
        foreach (var type in _knownTypes)
        {
            var mask = JavaCodeMask.Blank(body);
            var matches = Regex.Matches(mask, @"\b" + Regex.Escape(type) + @"\b", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            foreach (var match in matches.Reverse()) body = body.Remove(match.Index, match.Length).Insert(match.Index, builder.Reference(type));
        }
        return body;
    }

    static string? ImportsFailure(string code)
    {
        foreach (var type in _knownTypes)
        {
            if (!code.Contains(type, StringComparison.Ordinal)) continue;
            var simple = type[(type.LastIndexOf('.') + 1)..];
            if (Match(code.Replace(type, string.Empty, StringComparison.Ordinal), @"\b" + simple + @"\b"))
                return "An authored type name conflicts with a required native import.";
            code = code.Replace(type, simple, StringComparison.Ordinal);
        }
        return Match(code, @"\b(?:java|javax|org|com|io)\.[A-Za-z_][\w.]*")
            ? "An authored qualified type cannot be safely imported." : null;
    }

    static string? ClaimsFailure(AxonEmissionContext context) => HttpAuthenticationInput.Bearer(context.Request.Profile)
        ? null : "Implicit claims require a native JWT authentication provider.";

    static bool Match(string source, string pattern) => Regex.IsMatch(source, pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
}
