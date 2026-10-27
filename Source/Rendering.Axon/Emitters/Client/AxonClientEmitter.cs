// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters.Specifications;

namespace Cratis.Stage.Rendering.Axon.Emitters.Client;

/// <summary>Emits the opt-in TypeScript client: typed descriptors and decoders over a transport-neutral seam, plus an HTTP transport for the generated routes.</summary>
internal sealed class AxonClientEmitter : IAxonEmitter
{
    const string Prefix = "Cratis.Stage.Rendering.Axon.Emitters.Client.Templates.";

    const string TsConfig = """
        {
          "compilerOptions": {
            "target": "ES2022",
            "module": "ESNext",
            "moduleResolution": "Bundler",
            "lib": ["ES2022", "DOM"],
            "types": [],
            "strict": true,
            "noEmit": true,
            "skipLibCheck": true,
            "allowImportingTsExtensions": true,
            "erasableSyntaxOnly": true,
            "verbatimModuleSyntax": true
          },
          "include": ["src/**/*.ts"]
        }

        """;

    static readonly JsonSerializerOptions _options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public bool IsImplemented => true;

    public void Emit(AxonEmissionContext context)
    {
        if (!TypeScriptClientInput.TryRead(context.Request.Profile, out var requested) || !requested) return;

        context.Artifacts.Add(PlannedArtifact.CreateText("client/tsconfig.json", TsConfig));
        EmitTemplates(context);
        context.Artifacts.Add(PlannedArtifact.CreateText("client/src/problems.ts", Problems()));

        var concepts = new TsFile(ClientTypes.ConceptsModule);
        var types = new ClientTypes(context, concepts);
        ClientConcepts.Write(context, concepts, types);
        context.Artifacts.Add(PlannedArtifact.CreateText("client/src/" + ClientTypes.ConceptsModule, concepts.Render()));

        var slices = new ClientSlices(context, types);
        var endpoints = AxonHttpTestsEndpoints.For(context).OrderBy(endpoint => endpoint.Kind == "stream").ToArray();
        var planned = endpoints.Select(endpoint => (Endpoint: endpoint, Typed: slices.Plan(endpoint))).ToArray();
        context.Artifacts.AddRange(slices.Write());
        context.Artifacts.Add(PlannedArtifact.CreateText("client/src/http-routes.ts", Routes(context, slices, planned)));
    }

    internal static string Key(HttpEndpoint endpoint) => $"{endpoint.Method} {endpoint.Path}" + (endpoint.Produces is null ? string.Empty : " " + endpoint.Produces);

    static void EmitTemplates(AxonEmissionContext context)
    {
        var assembly = typeof(AxonClientEmitter).Assembly;
        foreach (var resource in assembly.GetManifestResourceNames().Where(name => name.StartsWith(Prefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            context.Artifacts.Add(PlannedArtifact.CreateText("client/src/" + resource[Prefix.Length..], reader.ReadToEnd()));
        }
    }

    static string Problems()
    {
        var representatives = new HttpEndpoint[]
        {
            new("command", "POST", string.Empty, null, null, [], null),
            new("stream", "GET", string.Empty, "text/event-stream", null, [], "many"),
            new("query", "GET", string.Empty, null, null, [], "one"),
            new("query", "GET", string.Empty, null, null, [], "zeroOrOne")
        };
        var types = representatives.SelectMany(AxonHttpContractJson.Problems).Select(problem => problem.Type).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var text = new StringBuilder("/** The problem type URNs the generated server answers with, as listed in http-contract.json. */\n")
            .Append("export const problemTypes = {\n");
        foreach (var type in types) text.Append("    ").Append(Camel(type)).Append(": ").Append(ClientNames.Literal("urn:cratis:stage-axon:problem:" + type)).Append(",\n");
        return text.Append("} as const;\n").ToString();
    }

    static string Camel(string kebab)
    {
        var words = kebab.Split('-');
        return words[0] + string.Concat(words.Skip(1).Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
    }

    static string Routes(AxonEmissionContext context, ClientSlices slices, (HttpEndpoint Endpoint, bool Typed)[] planned)
    {
        var text = new StringBuilder("import type { Route } from \"./http-transport.ts\";\n\nexport const routes: readonly Route[] = [\n");
        foreach (var (endpoint, typed) in planned)
        {
            if (!typed)
            {
                var reason = $"{endpoint.Method} {endpoint.Path} has no modeled source, so the TypeScript client does not describe it";
                context.Degradation.Warn(AxonRefusalCodes.Client, context.Request.Model.Application.Id, reason + ".");
                text.Append("    // Skipped: ").Append(ClientNames.Comment(reason)).Append(".\n");
                continue;
            }

            text.Append("    ").Append(JsonSerializer.Serialize(Describe(endpoint, slices), _options)).Append(",\n");
        }

        return text.Append("];\n").ToString();
    }

    static Dictionary<string, object?> Describe(HttpEndpoint endpoint, ClientSlices slices)
    {
        var described = new Dictionary<string, object?> { ["id"] = Key(endpoint) };
        foreach (var (name, value) in AxonHttpContractJson.Describe(endpoint)) described[name] = value;
        described["response"] = endpoint.Kind switch
        {
            "stream" => "stream",
            "command" => slices.IsVoid(endpoint.Source!.Value) ? "none" : "json",
            _ => "json"
        };
        return described;
    }
}
