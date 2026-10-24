// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Java;

internal static class JavaNames
{
    static readonly HashSet<string> _reserved = new(
        "abstract assert boolean break byte case catch char class const continue default do double else enum extends final finally float for goto if implements import instanceof int interface long native new package private protected public return short static strictfp super switch synchronized this throw throws transient try void volatile while true false null _ exports module open opens provides requires to transitive uses with record sealed permits var yield when".Split(' '),
        StringComparer.Ordinal);

    internal static string Type(string name)
    {
        var words = new string([.. name.Select(character => char.IsAsciiLetterOrDigit(character) ? character : ' ')])
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return Protect(string.Concat(words.Select(word => char.ToUpperInvariant(word[0]) + word[1..])));
    }

    internal static string Member(string name)
    {
        var type = Type(name);
        return Protect(char.ToLowerInvariant(type[0]) + type[1..]);
    }

    internal static string PackageSegment(string name) => Protect(
        new string([.. name.ToLowerInvariant().Where(char.IsAsciiLetterOrDigit)]));

    static string Protect(string name)
    {
        if (name.Length == 0) return "generated";
        if (char.IsAsciiDigit(name[0])) name = "_" + name;
        return _reserved.Contains(name) ? name + "_" : name;
    }
}
