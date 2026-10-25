// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Events;

internal sealed class EventTagsEmitter : IAxonEmitter
{
    public bool IsImplemented => true;

    public void Emit(AxonEmissionContext context)
    {
        foreach (var module in AxonEventsEmitter.Contracts(context).Select(pair => pair.Module).DistinctBy(module => module.Id))
        {
            var contracts = Contracts(context, module).ToArray();
            var code = new JavaCodeBuilder(AxonEventsEmitter.Package(context, module))
                .Block("public final class EventTags", body =>
                {
                    foreach (var (key, name) in Constants(context, contracts).OrderBy(pair => pair.Value, StringComparer.Ordinal))
                        body.Line($"public static final String {name} = {JavaCodeBuilder.StringLiteral(key)};");
                    body.Line("private EventTags() {}");
                }).Build();
            context.AddJava(AxonEventsEmitter.Package(context, module), "EventTags", code, sources: [.. contracts.Select(contract => contract.Id)]);
        }
    }

    internal static string Reference(AxonEmissionContext context, SemanticEventContract contract)
    {
        var module = ScopeSelection.Locate(context.Request.Model.Application).First(slice => slice.Slice.Events.Any(candidate => candidate.Id == contract.Id)).Module;
        var key = AxonEventSources.PrimaryTag(context, contract).TagKey;
        return AxonEventsEmitter.Package(context, module) + ".EventTags." + Constants(context, Contracts(context, module))[key];
    }

    static IEnumerable<SemanticEventContract> Contracts(AxonEmissionContext context, SemanticModule module) =>
        ScopeSelection.Locate(context.Request.Model.Application).Where(slice => slice.Module.Id == module.Id)
            .SelectMany(slice => slice.Slice.Events).DistinctBy(contract => contract.Id);

    static Dictionary<string, string> Constants(AxonEmissionContext context, IEnumerable<SemanticEventContract> contracts) =>
        EventTagNames.Constants(contracts.Select(contract => AxonEventSources.PrimaryTag(context, contract).TagKey));
}
