// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Queries;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence.for_AxonPersistenceEmitter;

public class when_models_claim_persistence_framework_names : Projections.for_AxonProjectionsEmitter.given.a_view_request
{
    AxonEmissionContext _context;
    string _configuration;
    string _envelope;
    string _support;
    string _fixture;

    void Establish()
    {
        var application = _request.Model.Application;
        static SemanticFeature Rename(SemanticFeature feature) => feature with
        {
            Features = [.. feature.Features.Select(Rename)],
            Slices = [.. feature.Slices.Select(slice => slice with
            {
                ReadModels = [.. slice.ReadModels.Select(model => model.Name == "Total" ? model with { Name = "Set" } : model)],
                Queries = [.. slice.Queries.Select(query => query.Name == "AllTotals" ? query with { Name = "EventProcessorDefinition" } : query)],
                Events = [.. slice.Events.Select(@event => @event.Name switch
                {
                    "Increased" => @event with { Name = "Bean" },
                    "Decreased" => @event with { Name = "PropagatingErrorHandler" },
                    _ => @event
                })]
            })]
        };
        _request = _request with
        {
            Model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application with
            {
                Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(Rename)] })]
            })
        };
        _context = new(_request, new(_request.Profile));
    }

    void Because()
    {
        new AxonProjectionsEmitter().Emit(_context);
        new AxonQueriesEmitter().Emit(_context);
        new AxonPersistenceEmitter().Emit(_context);
        _configuration = Code("src/main/java/axonviews/persistence/PersistentViewConfiguration.java");
        _envelope = Code("src/test/java/axonviews/views/counters/totals/SetPersistentEnvelopeTest.java");
        _support = Code("src/test/java/axonviews/views/counters/totals/SetPersistentSupport.java");
        _fixture = Code("src/test/java/axonviews/views/counters/totals/SetPersistentFixture.java");
    }

    [Fact] void should_still_import_set() => _configuration.ShouldContain("import java.util.Set;");
    [Fact] void should_still_import_bean() => _configuration.ShouldContain("import org.springframework.context.annotation.Bean;");
    [Fact] void should_still_import_the_event_processor_definition() => _configuration.ShouldContain("import org.axonframework.extension.spring.config.EventProcessorDefinition;");
    [Fact] void should_still_import_the_propagating_error_handler() => _configuration.ShouldContain("import org.axonframework.messaging.eventhandling.processing.errorhandling.PropagatingErrorHandler;");
    [Fact]
    void should_use_the_framework_set()
    {
        _configuration.ShouldContain("Set.of(");
        _configuration.ShouldContain("\"axonviews.views.counters.totals\"");
    }
    [Fact] void should_use_the_framework_processor_definition() => _configuration.ShouldContain("EventProcessorDefinition.pooledStreamingMatching(");
    [Fact] void should_use_the_framework_error_handler() => _configuration.ShouldContain("config.errorHandler(PropagatingErrorHandler.INSTANCE)");
    [Fact] void should_not_declare_a_colliding_type_in_the_configuration() => _configuration.ShouldNotContain("class Set");
    [Fact]
    void should_import_each_framework_simple_name_once() => new[] { "Set", "Bean", "EventProcessorDefinition", "PropagatingErrorHandler" }
        .ShouldEachConformTo(name => _configuration.Split('\n').Count(line => line.StartsWith("import ", StringComparison.Ordinal) && line.EndsWith("." + name + ";", StringComparison.Ordinal)) == 1);
    [Fact] void should_keep_the_axon_configuration_qualified() => _configuration.ShouldContain("persistentViewProcessorGuard(org.axonframework.common.configuration.Configuration configuration)");
    [Fact] void should_not_import_the_axon_configuration_in_the_configuration() => _configuration.ShouldNotContain("import org.axonframework.common.configuration.Configuration;");
    [Fact] void should_reference_the_modeled_set_qualified_in_the_support() => _support.ShouldContain("ReadModelStore<Object, axonviews.views.counters.totals.Set> store(");
    [Fact] void should_not_reference_the_modeled_set_by_short_name_in_the_support() => _support.ShouldNotContain("<Object, Set>");
    [Fact] void should_shorten_the_modeled_bean_event_without_a_fixture_collision() => _fixture.ShouldContain("new Bean(");
    [Fact] void should_import_the_modeled_bean_event_without_a_fixture_collision() => _fixture.ShouldContain("import axonviews.views.events.Bean;");
    [Fact] void should_keep_the_modeled_bean_qualified_when_the_support_imports_the_framework_annotation() => _support.ShouldContain("new axonviews.views.events.Bean(");
    [Fact] void should_keep_the_framework_processor_definition_in_the_envelope_proof() => _envelope.ShouldContain("import org.axonframework.extension.spring.config.EventProcessorDefinition;");

    string Code(string path) => Encoding.UTF8.GetString(_context.Artifacts.Single(artifact => artifact.RelativePath == path).Bytes.AsSpan());
}
