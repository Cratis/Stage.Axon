// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Emitters.Queries;
using Cratis.Stage.Rendering.Axon.Emitters.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Client;

/// <summary>Plans and writes one TypeScript module per slice that owns an endpoint or a read model an endpoint returns.</summary>
internal sealed class ClientSlices
{
    readonly AxonEmissionContext _context;
    readonly ClientTypes _types;
    readonly Dictionary<SemanticId, (LocatedSemanticSlice Located, SemanticKeyedQuery Query)> _queries = [];
    readonly Dictionary<SemanticId, (LocatedSemanticSlice Located, SemanticCommand Command)> _commands = [];
    readonly Dictionary<string, SliceFile> _files = new(StringComparer.Ordinal);
    readonly Dictionary<SemanticId, ReadModelSite> _models = [];
    readonly Dictionary<SemanticId, QuerySite> _querySites = [];
    readonly Dictionary<SemanticId, CommandSite> _commandSites = [];

    internal ClientSlices(AxonEmissionContext context, ClientTypes types)
    {
        _context = context;
        _types = types;
        foreach (var located in ScopeSelection.Select(context.Request))
        {
            foreach (var query in located.Slice.Queries.Concat(QueryPerformer.Queries(context, located))) _queries.TryAdd(query.Id, (located, query));
            foreach (var command in located.Slice.Commands) _commands.TryAdd(command.Id, (located, command));
        }
    }

    /// <summary>Gets whether the endpoint has been planned with enough semantic information to be typed.</summary>
    /// <param name="endpoint">The registered endpoint.</param>
    /// <returns>True when the endpoint can be typed.</returns>
    internal bool Plan(HttpEndpoint endpoint)
    {
        if (endpoint.Source is not { } source) return false;
        if (_commands.TryGetValue(source, out var command)) return PlanCommand(command.Located, command.Command, endpoint);
        return _queries.TryGetValue(source, out var query) && PlanQuery(query.Located, query.Query, endpoint);
    }

    /// <summary>Gets whether a command returns nothing.</summary>
    /// <param name="source">The command identifier.</param>
    /// <returns>True when the command has no response body.</returns>
    internal bool IsVoid(SemanticId source) => _commandSites.TryGetValue(source, out var site) && site.Command.Response is not (SemanticScalarCommandResponse or SemanticRecordCommandResponse);

    /// <summary>Writes every planned module.</summary>
    /// <returns>The module artifacts, ordered by path.</returns>
    internal IEnumerable<PlannedArtifact> Write()
    {
        foreach (var site in _commandSites.Values) DeclareCommand(site);
        foreach (var site in _querySites.Values) DeclareQuery(site);
        foreach (var slice in _files.Values.OrderBy(slice => slice.File.Path, StringComparer.Ordinal))
        {
            foreach (var model in slice.Models) WriteReadModel(slice, model);
            foreach (var site in _querySites.Values.Where(site => site.Slice == slice)) WriteQuery(site);
            foreach (var site in _commandSites.Values.Where(site => site.Slice == slice)) WriteCommand(site);
        }

        return _files.Values.OrderBy(slice => slice.File.Path, StringComparer.Ordinal)
            .Select(slice => PlannedArtifact.CreateText("client/src/" + slice.File.Path, slice.File.Render(), [.. slice.Sources.Distinct()]));
    }

    bool PlanCommand(LocatedSemanticSlice located, SemanticCommand command, HttpEndpoint endpoint)
    {
        if (_commandSites.ContainsKey(command.Id)) return true;
        _commandSites[command.Id] = new(File(located), command, endpoint, new());
        File(located).Sources.Add(command.Id);
        return true;
    }

    bool PlanQuery(LocatedSemanticSlice located, SemanticKeyedQuery query, HttpEndpoint endpoint)
    {
        if (_querySites.ContainsKey(query.Id)) return true;
        var owner = ScopeSelection.Locate(_context.Request.Model.Application).FirstOrDefault(slice => slice.Slice.ReadModels.Any(model => model.Id == query.ReadModel));
        var model = owner?.Slice.ReadModels.First(candidate => candidate.Id == query.ReadModel);
        if (owner is null || model is null) return false;
        var ownerFile = File(owner);
        if (!_models.TryGetValue(model.Id, out var site))
        {
            var type = ownerFile.File.Declare(ClientNames.Type(model.Name));
            _models[model.Id] = site = new(ownerFile, model, type, ownerFile.File.Declare("decode" + type));
            ownerFile.Models.Add(model);
            ownerFile.Sources.Add(model.Id);
        }

        var slice = File(located);
        slice.Sources.Add(query.Id);
        _querySites[query.Id] = new(slice, query, endpoint, site, new());
        return true;
    }

    SliceFile File(LocatedSemanticSlice located)
    {
        var path = string.Join('/', located.Path.Select(ClientNames.Segment)) + ".ts";
        if (!_files.TryGetValue(path, out var file)) _files[path] = file = new(new TsFile(path));
        return file;
    }

    void DeclareCommand(CommandSite site)
    {
        var file = site.Slice.File;
        site.Names.Body = file.Declare(ClientNames.Type(site.Command.Name));
        site.Names.Descriptor = file.Declare(ClientNames.Value(site.Command.Name));
        if (site.Command.Response is SemanticRecordCommandResponse)
        {
            site.Names.Response = file.Declare(ClientNames.Type(site.Command.Name) + "Response");
            site.Names.Decoder = file.Declare("decode" + site.Names.Response);
        }
    }

    void DeclareQuery(QuerySite site)
    {
        site.Names.Body = site.Slice.File.Declare(ClientNames.Type(site.Query.Name) + "Key");
        site.Names.Descriptor = site.Slice.File.Declare(ClientNames.Value(site.Query.Name));
    }

    void WriteReadModel(SliceFile slice, SemanticReadModel model)
    {
        var site = _models[model.Id];
        var members = model.Properties.Select(property => new TsMember(property.Name, _types.Resolve(slice.File, property.Type, property.Id, $"{model.Name}.{property.Name}"))).ToArray();
        ClientShapes.Interface(slice.File, site.Type, members);
        ClientShapes.Decoder(slice.File, site.Decoder, site.Type, members);
    }

    void WriteQuery(QuerySite site)
    {
        var file = site.Slice.File;
        var members = site.Endpoint.Parameters.Select(parameter => new TsMember(
            parameter.Name,
            parameter.Type is { } type
                ? _types.Resolve(file, type with { IsOptional = false, IsCollection = false }, site.Query.Id, $"{site.Query.Name} argument {parameter.Name}", false)
                : _types.Unknown(file, site.Query.Id, $"{site.Query.Name} argument {parameter.Name}", "the argument has no modeled type", false),
            !parameter.Required)).ToArray();
        ClientShapes.Interface(file, site.Names.Body, members);
        var model = site.Model;
        var type = file.Import(model.Slice.File.Path, model.Type, true);
        var decoder = file.Import(model.Slice.File.Path, model.Decoder);
        var cardinality = site.Endpoint.Cardinality ?? "many";
        var result = cardinality switch { "many" => type + "[]", "one" => type, _ => type + " | null" };
        var decode = cardinality == "many" ? $"{file.Import("transport.ts", "arrayOf")}({decoder})" : decoder;
        var descriptor = file.Import("transport.ts", "QueryDescriptor", true);
        file.Line($"export const {site.Names.Descriptor}: {descriptor}<{site.Names.Body}, {result}> = {{")
            .Line($"    id: {ClientNames.Literal($"{site.Endpoint.Method} {site.Endpoint.Path}")},")
            .Line($"    cardinality: {ClientNames.Literal(cardinality)},")
            .Line($"    live: {(site.Query.Delivery == SemanticQueryDelivery.Live ? "true" : "false")},")
            .Line($"    decode: {decode},");
        if (cardinality == "zeroOrOne") file.Line("    absent: () => null,");
        file.Line("};").Line();
    }

    void WriteCommand(CommandSite site)
    {
        var file = site.Slice.File;
        var command = site.Command;
        var members = command.Properties.Where(property => !property.IsGenerated)
            .Select(property => new TsMember(property.Name, _types.Resolve(file, property.Type, property.Id, $"{command.Name}.{property.Name}", false), property.Type.IsOptional)).ToArray();
        ClientShapes.Interface(file, site.Names.Body, members);
        var (result, decode) = Response(site);
        var descriptor = file.Import("transport.ts", "CommandDescriptor", true);
        file.Line($"export const {site.Names.Descriptor}: {descriptor}<{site.Names.Body}, {result}> = {{")
            .Line($"    id: {ClientNames.Literal($"{site.Endpoint.Method} {site.Endpoint.Path}")},");
        if (decode is not null) file.Line($"    decode: {decode},");
        file.Line("};").Line();
    }

    (string Result, string? Decode) Response(CommandSite site)
    {
        var file = site.Slice.File;
        var command = site.Command;
        switch (command.Response)
        {
            case SemanticScalarCommandResponse scalar:
                var type = _types.Resolve(file, scalar.Type, command.Id, $"{command.Name} response");
                return (type.Type, type.Decoder);
            case SemanticRecordCommandResponse record:
                var fields = record.Fields.Select(field => new TsMember(field.Name, _types.Resolve(file, field.Type, command.Id, $"{command.Name}.{field.Name}"))).ToArray();
                ClientShapes.Interface(file, site.Names.Response, fields);
                ClientShapes.Decoder(file, site.Names.Decoder, site.Names.Response, fields);
                return (site.Names.Response, site.Names.Decoder);
            default:
                return ("void", null);
        }
    }

    sealed class SliceFile(TsFile file)
    {
        internal TsFile File { get; } = file;

        internal List<SemanticReadModel> Models { get; } = [];

        internal List<SemanticId> Sources { get; } = [];
    }

    sealed class Names
    {
        internal string Body { get; set; } = string.Empty;

        internal string Descriptor { get; set; } = string.Empty;

        internal string Response { get; set; } = string.Empty;

        internal string Decoder { get; set; } = string.Empty;
    }

    sealed record ReadModelSite(SliceFile Slice, SemanticReadModel Model, string Type, string Decoder);

    sealed record QuerySite(SliceFile Slice, SemanticKeyedQuery Query, HttpEndpoint Endpoint, ReadModelSite Model, Names Names);

    sealed record CommandSite(SliceFile Slice, SemanticCommand Command, HttpEndpoint Endpoint, Names Names);
}
