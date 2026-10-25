// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text.RegularExpressions;

namespace Cratis.Stage.Rendering.Axon.Emitters.Events;

internal static partial class EventTagNames
{
    [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex WordBoundary { get; }

    [GeneratedRegex("[^A-Za-z0-9]+", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex NonIdentifier { get; }

    internal static Dictionary<string, string> Constants(IEnumerable<string> keys)
    {
        var constants = new Dictionary<string, string>(StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in keys.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var name = Name(key);
            var candidate = name;
            for (var suffix = 2; !used.Add(candidate); suffix++)
                candidate = name + "_" + suffix.ToString(CultureInfo.InvariantCulture);
            constants.Add(key, candidate);
        }
        return constants;
    }

    static string Name(string key)
    {
        var words = WordBoundary.Replace(key, "_");
        var name = NonIdentifier.Replace(words, "_").Trim('_').ToUpperInvariant();
        if (name.Length == 0) return "TAG";
        return char.IsAsciiDigit(name[0]) ? "_" + name : name;
    }
}
