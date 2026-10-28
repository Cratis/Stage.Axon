package io.cratis.stage.axon.starter;

import io.cratis.stage.axon.runtime.UniqueValueStore;
import io.cratis.stage.axon.runtime.persistent.JpaUniqueValueStore;
import org.axonframework.common.configuration.Configuration;
import org.axonframework.common.configuration.ConfigurationEnhancer;
import org.axonframework.common.jdbc.PersistenceExceptionResolver;
import org.axonframework.eventsourcing.eventstore.EventStorageEngine;
import org.axonframework.eventsourcing.eventstore.jpa.AggregateBasedJpaEventStorageEngine;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.context.ApplicationContext;
import org.springframework.test.context.ActiveProfiles;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertNotSame;
import static org.junit.jupiter.api.Assertions.assertTrue;

@SpringBootTest(classes = StarterFixtureApplication.class, properties = "spring.datasource.url=jdbc:h2:mem:s4starterpersistent;DB_CLOSE_DELAY=-1")
@ActiveProfiles("persistent")
class StarterPersistentProfileTest {
    @Autowired ApplicationContext context;
    @Autowired Configuration configuration;

    @Test
    void resolvesTheJpaEngine() {
        assertInstanceOf(AggregateBasedJpaEventStorageEngine.class, configuration.getComponent(EventStorageEngine.class));
    }

    @Test
    void resolvesTheModuleEngineNotAxons() {
        var descriptor = new RecordingComponentDescriptor();
        configuration.getComponent(EventStorageEngine.class).describeTo(descriptor);
        var resolver = descriptor.property("persistenceExceptionResolver");
        assertTrue(descriptor.hasProperty("persistenceExceptionResolver"));
        assertNotSame(context.getBean(PersistenceExceptionResolver.class), resolver);
    }

    @Test
    void bothEnhancersAreRegisteredAndOursRunsFirst() {
        var ours = context.getBean("eventStorageEngine", ConfigurationEnhancer.class);
        var axons = context.getBean("aggregateBasedJpaEventStorageEngine", ConfigurationEnhancer.class);
        assertTrue(ours.order() < axons.order());
    }

    @Test
    void providesExactlyTheJpaUniqueValueStore() {
        var stores = context.getBeansOfType(UniqueValueStore.class);
        assertEquals(1, stores.size());
        assertInstanceOf(JpaUniqueValueStore.class, stores.values().iterator().next());
    }
}
