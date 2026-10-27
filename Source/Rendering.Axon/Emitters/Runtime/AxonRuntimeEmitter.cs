// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters.Security;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Runtime;

internal sealed class AxonRuntimeEmitter : IAxonEmitter
{
    const string Prefix = "Cratis.Stage.Rendering.Axon.Emitters.Runtime.Templates.";

    public bool IsImplemented => true;

    public void Emit(AxonEmissionContext context)
    {
        var bearer = HttpAuthenticationInput.Bearer(context.Request.Profile);
        var roles = DemoIdentityRoles.From(context);
        var rolePolicy = roles.Length == 0 ? "denyAll()" : "hasRole('" + roles[0].Replace("'", "''", StringComparison.Ordinal) + "')";
        var assembly = typeof(AxonRuntimeEmitter).Assembly;
        foreach (var resource in assembly.GetManifestResourceNames().Where(name => name.StartsWith(Prefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            var relative = resource[Prefix.Length..^".java".Length];
            if (bearer ? relative == "test.HttpIdentityContractTest" || relative == "test.HttpBrowserContractTest" : relative.StartsWith("test.HttpBearer", StringComparison.Ordinal)) continue;
            var test = relative.StartsWith("test.", StringComparison.Ordinal);
            if (test) relative = relative["test.".Length..];
            var separator = relative.LastIndexOf('.');
            var package = AxonRuntimePackage.JavaPackage(context) + (separator < 0 ? string.Empty : "." + relative[..separator]);
            var name = relative[(separator + 1)..];
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            var source = reader.ReadToEnd().Replace("__RUNTIME__", AxonRuntimePackage.JavaPackage(context), StringComparison.Ordinal)
                .Replace("__SECURITY__", context.BasePackage + ".infrastructure", StringComparison.Ordinal)
                .Replace("__DEMO_AUTHORITY_TEXT__", JavaCodeBuilder.StringLiteral(string.Join(',', roles.Select(role => "ROLE_" + role))), StringComparison.Ordinal)
                .Replace("__DEMO_ROLE_POLICY__", JavaCodeBuilder.StringLiteral(rolePolicy), StringComparison.Ordinal);
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
