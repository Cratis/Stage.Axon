// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Emitters.Security;

/// <summary>
/// Blanks Java comments, string literals, char literals and text blocks so that code can be inspected without matching their contents.
/// </summary>
internal static class JavaCodeMask
{
    const string TextBlockDelimiter = "\"\"\"";

    /// <summary>
    /// Replaces every non-code character with a space, keeping line breaks and length.
    /// </summary>
    /// <param name="source">The Java source.</param>
    /// <returns>The source with comments and literals blanked.</returns>
    internal static string Blank(string source)
    {
        var chars = source.ToCharArray();
        for (var index = 0; index < chars.Length;)
        {
            var end = NonCodeEnd(source, index);
            if (end < 0) index++;
            else index = BlankRange(chars, index, end);
        }

        return new string(chars);
    }

    static int BlankRange(char[] chars, int start, int end)
    {
        for (var index = start; index < end; index++)
        {
            if (chars[index] is not ('\n' or '\r')) chars[index] = ' ';
        }

        return end;
    }

    // Returns the exclusive end of the comment or literal starting at index, or -1 when index is plain code.
    static int NonCodeEnd(string source, int index) => source[index] switch
    {
        '"' when string.CompareOrdinal(source, index, TextBlockDelimiter, 0, 3) == 0 => ClosingEnd(source, index + 3, TextBlockDelimiter),
        '"' => ClosingEnd(source, index + 1, "\""),
        '\'' => ClosingEnd(source, index + 1, "'"),
        '/' when string.CompareOrdinal(source, index, "//", 0, 2) == 0 => LineEnd(source, index),
        '/' when string.CompareOrdinal(source, index, "/*", 0, 2) == 0 => BlockCommentEnd(source, index),
        _ => -1
    };

    // A backslash escapes the next character, so an escaped delimiter never closes the literal. An unterminated literal runs to the end.
    static int ClosingEnd(string source, int from, string delimiter)
    {
        for (var index = from; index < source.Length; index++)
        {
            if (source[index] == '\\') index++;
            else if (string.CompareOrdinal(source, index, delimiter, 0, delimiter.Length) == 0) return index + delimiter.Length;
        }

        return source.Length;
    }

    static int LineEnd(string source, int index)
    {
        var end = source.IndexOf('\n', index);
        return end < 0 ? source.Length : end;
    }

    static int BlockCommentEnd(string source, int index)
    {
        var end = source.IndexOf("*/", index + 2, StringComparison.Ordinal);
        return end < 0 ? source.Length : end + 2;
    }
}
