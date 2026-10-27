package __RUNTIME__;

import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.context.annotation.Profile;

@Configuration
@Profile("!persistent")
public class InMemoryUniqueConfiguration {
    @Bean
    UniqueValueStore uniqueValueStore() { return new InMemoryUniqueValueStore(); }
}
