// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Emitters.Runtime;

internal sealed class AxonRuntimeEmitter : IAxonEmitter
{
    const string Prefix = "Cratis.Stage.Rendering.Axon.Emitters.Runtime.Templates.";

    public bool IsImplemented => true;

    public void Emit(AxonEmissionContext context)
    {
        var assembly = typeof(AxonRuntimeEmitter).Assembly;
        foreach (var resource in assembly.GetManifestResourceNames().Where(name => name.StartsWith(Prefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            var relative = resource[Prefix.Length..^".java".Length];
            var test = relative.StartsWith("test.", StringComparison.Ordinal);
            if (test) relative = relative["test.".Length..];
            var separator = relative.LastIndexOf('.');
            var package = context.BasePackage + ".runtime" + (separator < 0 ? string.Empty : "." + relative[..separator]);
            var name = relative[(separator + 1)..];
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            context.AddJava(package, name, reader.ReadToEnd().Replace("__RUNTIME__", context.BasePackage + ".runtime", StringComparison.Ordinal), test);
        }
    }
}
