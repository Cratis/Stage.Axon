// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

package __RUNTIME__;

@org.springframework.context.annotation.Configuration
@org.springframework.context.annotation.Profile("!persistent")
public class InMemoryUniqueConfiguration {
    @org.springframework.context.annotation.Bean
    UniqueValueStore uniqueValueStore() { return new InMemoryUniqueValueStore(); }
}
