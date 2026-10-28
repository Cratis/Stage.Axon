package io.cratis.stage.axon.runtime.views;

import io.cratis.stage.axon.runtime.persistent.UniqueClaim;
import jakarta.persistence.EntityManagerFactory;
import java.util.Map;
import java.util.Set;
import java.util.UUID;
import org.axonframework.common.configuration.Configuration;
import org.axonframework.eventsourcing.eventstore.jpa.AggregateEventEntry;
import org.axonframework.messaging.eventhandling.processing.EventProcessor;
import org.axonframework.messaging.eventhandling.processing.streaming.StreamingEventProcessor;
import org.axonframework.messaging.eventhandling.processing.streaming.token.store.jpa.TokenEntry;
import org.junit.jupiter.api.Test;
import org.springframework.boot.SpringBootConfiguration;
import org.springframework.boot.autoconfigure.EnableAutoConfiguration;
import org.springframework.boot.builder.SpringApplicationBuilder;
import org.springframework.boot.persistence.autoconfigure.EntityScan;
import org.springframework.context.ConfigurableApplicationContext;
import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

class ViewFixtureTest {
    @SpringBootConfiguration
    @EnableAutoConfiguration
    @EntityScan(basePackageClasses = { ViewRow.class, ViewGeneration.class, UniqueClaim.class, AggregateEventEntry.class, TokenEntry.class })
    static class Application {}

    static ConfigurableApplicationContext start(String database) {
        return new SpringApplicationBuilder(Application.class).profiles("persistent")
                .properties("spring.datasource.url=" + database, "spring.jpa.hibernate.ddl-auto=create-drop").run();
    }

    @Test
    void recordsGenerationAndRefusesAnIncompatibleFingerprintWithoutChangingIt() {
        try (var application = start("jdbc:h2:mem:" + UUID.randomUUID())) {
            var factory = application.getBean(EntityManagerFactory.class);
            PersistentViewBootstrap.verify(factory, "fixture", "first");
            PersistentViewBootstrap.verify(factory, "fixture", "first");
            var failure = assertThrows(IllegalStateException.class, () -> PersistentViewBootstrap.verify(factory, "fixture", "changed"));
            assertTrue(failure.getMessage().contains("STAGE-AXON-0308"));
            try (var manager = factory.createEntityManager()) {
                assertEquals("first", manager.find(ViewGeneration.class, JpaReadModelStore.digest("fixture")).fingerprint());
            }
        }
    }

    @Test
    void refusesRetainedRowsWithoutAGeneration() {
        try (var application = start("jdbc:h2:mem:" + UUID.randomUUID())) {
            var factory = application.getBean(EntityManagerFactory.class);
            try (var manager = factory.createEntityManager()) {
                manager.getTransaction().begin();
                manager.persist(new ViewRow("row", "fixture", "identity", "{}"));
                manager.getTransaction().commit();
            }
            var failure = assertThrows(IllegalStateException.class, () -> PersistentViewBootstrap.verify(factory, "fixture", "first"));
            assertTrue(failure.getMessage().contains("STAGE-AXON-0308"));
            try (var manager = factory.createEntityManager()) {
                assertNull(manager.find(ViewGeneration.class, JpaReadModelStore.digest("fixture")));
                assertNotNull(manager.find(ViewRow.class, "row"));
            }
        }
    }

    @Test
    void refusesMissingProcessors() {
        var configuration = mock(Configuration.class);
        when(configuration.getModuleConfigurations()).thenReturn(java.util.List.of());
        assertThrows(IllegalStateException.class, () -> PersistentViewProcessorGuard.verify(configuration, Set.of("required")));
    }

    @Test
    void refusesNonPooledProcessors() {
        var configuration = mock(Configuration.class);
        var processor = mock(org.axonframework.messaging.eventhandling.processing.streaming.pooled.PooledStreamingEventProcessorConfiguration.class);
        when(processor.processorName()).thenReturn("required");
        when(processor.errorHandler()).thenReturn(org.axonframework.messaging.eventhandling.processing.errorhandling.PropagatingErrorHandler.INSTANCE);
        when(configuration.getOptionalComponent(org.axonframework.messaging.eventhandling.processing.streaming.pooled.PooledStreamingEventProcessorConfiguration.class)).thenReturn(java.util.Optional.of(processor));
        when(configuration.getModuleConfigurations()).thenReturn(java.util.List.of());
        when(configuration.getComponents(StreamingEventProcessor.class)).thenReturn(Map.of("required", mock(StreamingEventProcessor.class)));
        when(configuration.getComponents(EventProcessor.class)).thenReturn(Map.of());
        assertThrows(IllegalStateException.class, () -> PersistentViewProcessorGuard.verify(configuration, Set.of("required")));
    }
}
