// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Queries;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence.for_AxonPersistenceEmitter;

public class when_emitting_short_persistence_names : Projections.for_AxonProjectionsEmitter.given.a_view_request
{
    AxonEmissionContext _context;
    string _configuration;
    string _bootstrap;
    string _store;

    void Because()
    {
        _context = new(_request, new(_request.Profile));
        new AxonProjectionsEmitter().Emit(_context);
        new AxonQueriesEmitter().Emit(_context);
        new AxonPersistenceEmitter().Emit(_context);
        _configuration = Code("PersistentViewConfiguration");
        _bootstrap = Code("PersistentViewBootstrap");
        _store = Code("TotalPersistentStoreTest");
    }

    [Fact] void should_import_bean() => _configuration.ShouldContain("import org.springframework.context.annotation.Bean;");
    [Fact] void should_import_the_event_processor_definition() => _configuration.ShouldContain("import org.axonframework.extension.spring.config.EventProcessorDefinition;");
    [Fact] void should_import_the_propagating_error_handler() => _configuration.ShouldContain("import org.axonframework.messaging.eventhandling.processing.errorhandling.PropagatingErrorHandler;");
    [Fact] void should_import_the_smart_initializing_singleton() => _configuration.ShouldContain("import org.springframework.beans.factory.SmartInitializingSingleton;");
    [Fact] void should_import_set() => _configuration.ShouldContain("import java.util.Set;");
    [Fact] void should_use_the_short_bean_annotation() => _configuration.ShouldContain("    @Bean\n    EventProcessorDefinition persistentViewProcessor0()");
    [Fact] void should_use_the_short_processor_definition() => _configuration.ShouldContain("return EventProcessorDefinition.pooledStreamingMatching(");
    [Fact] void should_use_the_short_error_handler() => _configuration.ShouldContain("config.errorHandler(PropagatingErrorHandler.INSTANCE)");
    [Fact] void should_use_the_short_singleton() => _configuration.ShouldContain("SmartInitializingSingleton persistentViewProcessorGuard(");
    [Fact] void should_use_the_short_set() => _configuration.ShouldContain("PersistentViewProcessorGuard.verify(configuration, Set.of(");
    [Fact] void should_keep_the_axon_configuration_qualified() => _configuration.ShouldContain("persistentViewProcessorGuard(org.axonframework.common.configuration.Configuration configuration)");
    [Fact] void should_keep_the_spring_configuration_import() => _configuration.ShouldContain("import org.springframework.context.annotation.Configuration;");
    [Fact] void should_not_import_the_axon_configuration() => _configuration.ShouldNotContain("import org.axonframework.common.configuration.Configuration;");
    [Fact] void should_not_qualify_the_framework_types_in_the_body() => _configuration.ShouldNotContain("@org.springframework.context.annotation.Bean");
    [Fact] void should_import_stream_in_the_bootstrap() => _bootstrap.ShouldContain("import java.util.stream.Stream;");
    [Fact] void should_use_the_short_stream_in_the_bootstrap() => _bootstrap.ShouldContain("return Stream.of(AggregateEventEntry.class");

    [Fact] void should_import_mockito_in_the_store_proof() => _store.ShouldContain("import org.mockito.Mockito;");
    [Fact] void should_use_short_mockito_in_the_store_proof() => _store.ShouldContain("JpaReadModelStore.bind(left, Mockito.mock(ProcessingContext.class))");
    [Fact] void should_import_completion_exception_in_the_store_proof() => _store.ShouldContain("import java.util.concurrent.CompletionException;");
    [Fact] void should_use_short_completion_exception_in_the_store_proof() => _store.ShouldContain("assertThrows(CompletionException.class, refusal::join)");

    string Code(string name) => Encoding.UTF8.GetString(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith('/' + name + ".java", StringComparison.Ordinal)).Bytes.AsSpan());
}
