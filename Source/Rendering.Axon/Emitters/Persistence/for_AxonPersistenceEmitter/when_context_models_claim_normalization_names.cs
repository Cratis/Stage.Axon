// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence.for_AxonPersistenceEmitter;

public class when_context_models_claim_normalization_names : for_PersistentViewAdmission.given.a_flat_view
{
    readonly Dictionary<string, AxonEmissionContext> _contexts = new(StringComparer.Ordinal);

    void Because()
    {
        foreach (var name in new[] { "Set", "List", "Collection", "Collectors" })
        {
            _contexts[name] = Render(slice => slice with
            {
                ReadModels = [.. slice.ReadModels.Select(model => model.Name == "ContextTotal" ? model with { Name = name } : model)],
                Events = [.. slice.Events.Select(contract => contract.Name == "ContextRecorded" ? contract with { Name = name } : contract)],
                Queries = [.. slice.Queries.Select(query => query.Name == "AllContextTotals" ? query with { Name = "Event" } : query)]
            });
        }
    }

    [Fact] void should_admit_each_context_mapping() => _contexts.Values.ShouldEachConformTo(context => PersistentViewAdmission.Supports(context, ReadModelId("ContextTotal")));
    [Fact] void should_warn_without_crashing() => _contexts.Keys.ShouldEachConformTo(name => Code(_contexts[name], name + "StoreConfiguration").Contains("STAGE-AXON-0308", StringComparison.Ordinal));
    [Fact] void should_keep_the_memory_store() => _contexts.Keys.ShouldEachConformTo(name => Code(_contexts[name], name + "StoreConfiguration").Contains("InMemoryReadModelStore", StringComparison.Ordinal));
    [Fact] void should_emit_persistent_proofs() => _contexts.Keys.ShouldEachConformTo(name => _contexts[name].Artifacts.Exists(artifact => artifact.RelativePath.Contains('/' + name + "Persistent", StringComparison.Ordinal)));
}
