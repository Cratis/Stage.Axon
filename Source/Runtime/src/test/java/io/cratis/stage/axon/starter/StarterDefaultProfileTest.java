package io.cratis.stage.axon.starter;

import io.cratis.stage.axon.runtime.InMemoryUniqueValueStore;
import io.cratis.stage.axon.runtime.UniqueValueStore;
import io.cratis.stage.axon.runtime.persistent.PersistentConfiguration;
import org.axonframework.common.configuration.Configuration;
import org.axonframework.eventsourcing.eventstore.EventStorageEngine;
import org.axonframework.eventsourcing.eventstore.inmemory.InMemoryEventStorageEngine;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.context.ApplicationContext;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertTrue;

@SpringBootTest(classes = StarterFixtureApplication.class, properties = "spring.datasource.url=jdbc:h2:mem:s4starterdefault;DB_CLOSE_DELAY=-1")
class StarterDefaultProfileTest {
    @Autowired ApplicationContext context;
    @Autowired Configuration configuration;

    @Test
    void keepsTheInMemoryEventStorageEngine() {
        assertInstanceOf(InMemoryEventStorageEngine.class, configuration.getComponent(EventStorageEngine.class));
    }

    @Test
    void keepsTheInMemoryUniqueValueStore() {
        assertInstanceOf(InMemoryUniqueValueStore.class, context.getBean(UniqueValueStore.class));
    }

    @Test
    void hasNoPersistentConfiguration() {
        assertTrue(context.getBeansOfType(PersistentConfiguration.class).isEmpty());
    }
}
