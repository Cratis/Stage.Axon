package io.cratis.stage.axon.runtime;

import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

@Configuration
public class InMemoryUniqueConfiguration {
    @Bean
    UniqueValueStore uniqueValueStore() { return new InMemoryUniqueValueStore(); }
}
