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

    [Fact]
    void should_qualify_the_normalized_model_constructors()
    {
        foreach (var name in _contexts.Keys)
        {
            Support(name).ShouldContain($"row = new axonviews.views.counters.contexttotals.{name}(row.name(), null, row.correlation(), row.details());");
            Support(name).ShouldContain($"row = new axonviews.views.counters.contexttotals.{name}(row.name(), row.occurred(), null, row.details());");
            Support(name).ShouldNotContain($"new {name}(");
        }
    }

    [Fact]
    void should_keep_the_framework_normalization_names()
    {
        foreach (var name in _contexts.Keys)
        {
            Support(name).ShouldContain("import java.util.Set;");
            Support(name).ShouldContain("import java.util.List;");
            Support(name).ShouldContain("import java.util.Collection;");
            Support(name).ShouldContain("import java.util.stream.Collectors;");
            Support(name).ShouldContain($"static Set<axonviews.views.counters.contexttotals.{name}> normalized(Collection<axonviews.views.counters.contexttotals.{name}> rows)");
            Support(name).ShouldContain($"static List<axonviews.views.counters.contexttotals.{name}> expected()");
            Support(name).ShouldContain(".collect(Collectors.toSet())");
            Support(name).ShouldNotContain($"import axonviews.views.counters.contexttotals.{name};");
            Support(name).ShouldNotContain($"import axonviews.views.events.{name};");
        }
    }

    [Fact]
    void should_keep_event_construction_unambiguous()
    {
        foreach (var name in _contexts.Keys)
            Support(name).ShouldContain($"new axonviews.views.events.{name}(");
    }

    [Fact]
    void should_keep_model_references_unambiguous_in_the_restart_proof()
    {
        foreach (var name in _contexts.Keys)
        {
            var proof = Code(_contexts[name], name + "PersistentTest");
            proof.ShouldContain("import java.util.Set;");
            proof.ShouldContain($"Set<{(name == "Set" ? "axonviews.views.counters.contexttotals.Set" : name)}> persisted;");
            proof.ShouldNotContain("import axonviews.views.counters.contexttotals.Set;");
        }
    }

    [Fact]
    void should_not_confuse_the_query_handler_with_the_event_handler_annotation()
    {
        foreach (var name in _contexts.Keys)
        {
            Support(name).ShouldContain("import org.axonframework.messaging.eventhandling.annotation.EventHandler;");
            Support(name).ShouldContain("axonviews.views.counters.contexttotals.EventHandler.class");
            Support(name).ShouldNotContain("import axonviews.views.counters.contexttotals.EventHandler;");
        }
    }

    string Support(string name) => Code(_contexts[name], name + "PersistentSupport");
}
