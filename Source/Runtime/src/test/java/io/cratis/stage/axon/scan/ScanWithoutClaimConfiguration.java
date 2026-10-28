package io.cratis.stage.axon.scan;

import org.axonframework.eventsourcing.eventstore.jpa.AggregateEventEntry;
import org.axonframework.messaging.eventhandling.processing.streaming.token.store.jpa.TokenEntry;
import org.springframework.boot.persistence.autoconfigure.EntityScan;
import org.springframework.context.annotation.Configuration;
import org.springframework.context.annotation.Profile;

@Configuration(proxyBeanMethods = false)
@Profile("persistent")
@EntityScan(basePackageClasses = { FixtureEntity.class, AggregateEventEntry.class, TokenEntry.class })
class ScanWithoutClaimConfiguration {}
