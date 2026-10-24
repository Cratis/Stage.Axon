// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace Cratis.Stage.Rendering.Axon.Java;

internal sealed class JavaCodeBuilder(string package)
{
    readonly SortedSet<string> _imports = new(StringComparer.Ordinal);
    readonly StringBuilder _body = new();
    int _indent;

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

    internal JavaCodeBuilder Import(string type)
    {
        _imports.Add(type);
        return this;
    }

    internal JavaCodeBuilder Line(string text = "")
    {
        if (text.Length > 0) _body.Append(' ', _indent * 4).Append(text);
        _body.Append('\n');
        return this;
    }

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
        var result = new StringBuilder("// Copyright (c) Cratis. All rights reserved.\n// Licensed under the MIT license. See LICENSE file in the project root for full license information.\n\n");
        result.Append("package ").Append(package).Append(";\n\n");
        foreach (var type in _imports) result.Append("import ").Append(type).Append(";\n");
        if (_imports.Count > 0) result.Append('\n');
        return result.Append(_body).ToString();
    }
}
