// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Rendering.Axon.Admission;

namespace Cratis.Stage.Rendering.Axon.Emitters.Runtime;

internal sealed class AxonRuntimeEmitter : IAxonEmitter
{
    const string Prefix = "Cratis.Stage.Rendering.Axon.Emitters.Runtime.Templates.";

    public bool IsImplemented => true;

    public void Emit(AxonEmissionContext context)
    {
        var bearer = HttpAuthenticationInput.Bearer(context.Request.Profile);
        var assembly = typeof(AxonRuntimeEmitter).Assembly;
        foreach (var resource in assembly.GetManifestResourceNames().Where(name => name.StartsWith(Prefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            var relative = resource[Prefix.Length..^".java".Length];
            if (bearer ? relative == "test.HttpIdentityContractTest" || relative == "test.HttpBrowserContractTest" : relative.StartsWith("test.HttpBearer", StringComparison.Ordinal)) continue;
            var test = relative.StartsWith("test.", StringComparison.Ordinal);
            if (test) relative = relative["test.".Length..];
            var separator = relative.LastIndexOf('.');
            var package = context.BasePackage + ".runtime" + (separator < 0 ? string.Empty : "." + relative[..separator]);
            var name = relative[(separator + 1)..];
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            var source = reader.ReadToEnd().Replace("__RUNTIME__", context.BasePackage + ".runtime", StringComparison.Ordinal)
                .Replace("__SECURITY__", context.BasePackage + ".infrastructure", StringComparison.Ordinal);
            context.AddJava(package, name, SortImports(source), test);
        }
    }

    static string SortImports(string source)
    {
        var lines = source.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            if (!lines[index].StartsWith("import ", StringComparison.Ordinal)) continue;
            var start = index;
            while (index < lines.Length && lines[index].StartsWith("import ", StringComparison.Ordinal)) index++;
            Array.Sort(lines, start, index - start, StringComparer.Ordinal);
        }
        return string.Join('\n', lines);
    }
}
