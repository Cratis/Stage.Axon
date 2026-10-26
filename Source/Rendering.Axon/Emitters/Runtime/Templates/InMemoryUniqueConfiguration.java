// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
