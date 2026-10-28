package io.cratis.stage.axon.runtime.persistent;

import io.cratis.stage.axon.runtime.UniqueValueStore;
import jakarta.persistence.EntityManagerFactory;
import org.axonframework.common.configuration.ComponentDefinition;
import org.axonframework.common.configuration.ConfigurationEnhancer;
import org.axonframework.common.configuration.SearchScope;
import org.axonframework.common.lifecycle.Phase;
import org.axonframework.eventsourcing.eventstore.EventStorageEngine;
import org.axonframework.eventsourcing.eventstore.jpa.AggregateBasedJpaEventStorageEngine;
import org.axonframework.messaging.core.unitofwork.transaction.jpa.JpaTransactionalExecutorProvider;
import org.axonframework.messaging.eventhandling.conversion.EventConverter;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.context.annotation.Profile;

@Configuration
@Profile("persistent")
public class PersistentConfiguration {
    @Bean
    ConfigurationEnhancer eventStorageEngine(EntityManagerFactory factory) {
        return registry -> registry.registerIfNotPresent(
                ComponentDefinition.ofType(EventStorageEngine.class)
                        .withBuilder(configuration -> new AggregateBasedJpaEventStorageEngine(
                                new JpaTransactionalExecutorProvider(factory),
                                configuration.getComponent(EventConverter.class),
                                config -> config))
                        .onShutdown(Phase.INBOUND_EVENT_CONNECTORS, engine -> {
                            if (engine instanceof AggregateBasedJpaEventStorageEngine jpaEngine) jpaEngine.close();
                        }),
                SearchScope.ALL);
    }

    @Bean
    UniqueValueStore uniqueValueStore(EntityManagerFactory factory) { return new JpaUniqueValueStore(factory); }
}
