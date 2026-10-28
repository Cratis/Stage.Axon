package io.cratis.stage.axon.starter;

import org.axonframework.common.configuration.Configuration;
import org.axonframework.common.jdbc.PersistenceExceptionResolver;
import org.axonframework.eventsourcing.eventstore.EventStorageEngine;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.context.ApplicationContext;
import org.springframework.test.context.ActiveProfiles;
import static org.junit.jupiter.api.Assertions.assertSame;

@SpringBootTest(classes = StarterFixtureApplication.class, properties = {
        "spring.datasource.url=jdbc:h2:mem:s4startercontrol;DB_CLOSE_DELAY=-1",
        "spring.autoconfigure.exclude=io.cratis.stage.axon.runtime.persistent.StageAxonPersistenceAutoConfiguration" })
@ActiveProfiles("persistent")
class StarterAxonOnlyControlTest {
    @Autowired ApplicationContext context;
    @Autowired Configuration configuration;

    @Test
    void resolvesAxonsEngineWithItsPersistenceExceptionResolver() {
        var descriptor = new RecordingComponentDescriptor();
        configuration.getComponent(EventStorageEngine.class).describeTo(descriptor);
        assertSame(context.getBean(PersistenceExceptionResolver.class), descriptor.property("persistenceExceptionResolver"));
    }
}
