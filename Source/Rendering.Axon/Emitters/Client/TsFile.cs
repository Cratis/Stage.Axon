// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Cratis.Stage.Rendering.Axon.Emitters.Client;

/// <summary>Builds one generated TypeScript module: it owns the module's declared names and resolves imports with short names, aliasing only on a collision.</summary>
/// <param name="path">The module path relative to the client source directory, for example <c>concepts.ts</c> or <c>sales/orders/place.ts</c>.</param>
internal sealed class TsFile(string path)
{
    readonly HashSet<string> _declared = new(StringComparer.Ordinal);
    readonly Dictionary<string, List<(string Name, string Local, bool IsType)>> _imports = new(StringComparer.Ordinal);
    readonly StringBuilder _body = new();

    internal string Path { get; } = path;

    /// <summary>Reserves a unique top-level name in this module.</summary>
    /// <param name="wanted">The preferred name.</param>
    /// <returns>The name, with a numeric suffix when the preferred one is taken.</returns>
    internal string Declare(string wanted)
    {
        var name = wanted;
        for (var index = 2; !_declared.Add(name); index++) name = wanted + index;
        return name;
    }

    /// <summary>Imports a name from another generated module.</summary>
    /// <param name="module">The module path relative to the client source directory.</param>
    /// <param name="name">The exported name.</param>
    /// <param name="isType">Whether the export is used as a type only.</param>
    /// <returns>The local name to use.</returns>
    internal string Import(string module, string name, bool isType = false)
    {
        if (module == Path) return name;
        if (!_imports.TryGetValue(module, out var entries)) _imports[module] = entries = [];
        var existing = entries.FindIndex(entry => entry.Name == name);
        if (existing >= 0)
        {
            var (_, local, wasType) = entries[existing];
            if (wasType && !isType) entries[existing] = (name, local, false);
            return local;
        }

        var alias = Declare(name);
        entries.Add((name, alias, isType));
        return alias;
    }

    internal TsFile Line(string text = "")
    {
        _body.Append(text).Append('\n');
        return this;
    }

    internal string Render()
    {
        var header = new StringBuilder();
        foreach (var (module, entries) in _imports.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var mixed = entries.Exists(entry => !entry.IsType);
            var names = entries.OrderBy(entry => entry.Name, StringComparer.Ordinal).Select(entry => (entry.IsType && mixed ? "type " : string.Empty) + (entry.Name == entry.Local ? entry.Name : entry.Name + " as " + entry.Local));
            header.Append(mixed ? "import" : "import type").Append(" { ").AppendJoin(", ", names).Append(" } from \"").Append(Relative(module)).Append("\";\n");
        }

        return header.Length == 0 ? _body.ToString() : header + "\n" + _body;
    }

    string Relative(string module)
    {
        var from = Path.Split('/')[..^1];
        var to = module.Split('/');
        var common = 0;
        while (common < from.Length && common < to.Length - 1 && from[common] == to[common]) common++;
        var up = Enumerable.Repeat("..", from.Length - common);
        var relative = string.Join('/', up.Concat(to.Skip(common)));
        return relative.StartsWith("..", StringComparison.Ordinal) ? relative : "./" + relative;
    }
}
