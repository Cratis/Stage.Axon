package io.cratis.stage.axon.starter;

import org.axonframework.eventsourcing.eventstore.EventStorageEngine;
import org.axonframework.eventsourcing.eventstore.inmemory.InMemoryEventStorageEngine;
import org.axonframework.messaging.eventhandling.processing.streaming.token.store.TokenStore;
import org.axonframework.messaging.eventhandling.processing.streaming.token.store.inmemory.InMemoryTokenStore;
import org.springframework.boot.SpringBootConfiguration;
import org.springframework.boot.autoconfigure.EnableAutoConfiguration;
import org.springframework.context.annotation.Bean;

@SpringBootConfiguration
@EnableAutoConfiguration
class StarterFixtureApplication {
    @Bean
    EventStorageEngine eventStorageEngine() { return new InMemoryEventStorageEngine(); }

    @Bean
    TokenStore tokenStore() { return new InMemoryTokenStore(); }
}
