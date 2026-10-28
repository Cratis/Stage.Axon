package io.cratis.stage.axon.scan;

import io.cratis.stage.axon.runtime.persistent.UniqueClaim;
import io.cratis.stage.axon.runtime.views.ViewRow;
import io.cratis.stage.axon.runtime.views.ViewGeneration;
import org.axonframework.eventsourcing.eventstore.jpa.AggregateEventEntry;
import org.axonframework.messaging.eventhandling.processing.streaming.token.store.jpa.TokenEntry;
import org.springframework.boot.persistence.autoconfigure.EntityScan;
import org.springframework.context.annotation.Configuration;
import org.springframework.context.annotation.Profile;

@Configuration(proxyBeanMethods = false)
@Profile("persistent")
@EntityScan(basePackageClasses = { FixtureEntity.class, ViewRow.class, ViewGeneration.class, UniqueClaim.class, AggregateEventEntry.class, TokenEntry.class })
class ExplicitScanConfiguration {}
