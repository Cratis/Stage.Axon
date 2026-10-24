// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

package __RUNTIME__.persistent;

import jakarta.persistence.EntityManagerFactory;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.context.annotation.Profile;
import org.axonframework.eventsourcing.eventstore.EventStorageEngine;
import org.axonframework.eventsourcing.eventstore.jpa.JpaEventStorageEngine;
import org.axonframework.eventsourcing.eventstore.jpa.JpaEventStorageEngineConfiguration;
import org.axonframework.messaging.core.unitofwork.transaction.jpa.JpaTransactionalExecutorProvider;
import __RUNTIME__.UniqueValueStore;

@Configuration
@Profile("persistent")
public class PersistentConfiguration {
    @Bean
    EventStorageEngine eventStorageEngine(EntityManagerFactory factory) {
        return new JpaEventStorageEngine(new JpaTransactionalExecutorProvider(factory), JpaEventStorageEngineConfiguration.DEFAULT);
    }

    @Bean
    UniqueValueStore uniqueValueStore(EntityManagerFactory factory) { return new JpaUniqueValueStore(factory); }
}
