package __RUNTIME__;

import io.cratis.stage.axon.runtime.ReadModelStore;
import io.cratis.stage.axon.runtime.UniqueValueConflict;
import io.cratis.stage.axon.runtime.UniqueValueStore;
import io.cratis.stage.axon.runtime.persistent.JpaUniqueValueStore;
import io.cratis.stage.axon.runtime.persistent.PersistentReadModelSafety;
import java.nio.file.Files;
import java.util.ArrayList;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.TimeUnit;
import org.axonframework.common.configuration.Configuration;
import org.axonframework.eventsourcing.annotation.EventTag;
import org.axonframework.eventsourcing.eventstore.EventStorageEngine;
import org.axonframework.eventsourcing.eventstore.SourcingCondition;
import org.axonframework.eventsourcing.eventstore.TerminalEventMessage;
import org.axonframework.eventsourcing.eventstore.jpa.AggregateBasedJpaEventStorageEngine;
import org.axonframework.messaging.core.unitofwork.UnitOfWorkFactory;
import org.axonframework.messaging.eventhandling.gateway.EventAppender;
import org.axonframework.messaging.eventhandling.processing.streaming.token.GlobalSequenceTrackingToken;
import org.axonframework.messaging.eventhandling.processing.streaming.token.store.TokenStore;
import org.axonframework.messaging.eventhandling.processing.streaming.token.store.jpa.JpaTokenStore;
import org.axonframework.messaging.eventstreaming.EventCriteria;
import org.axonframework.messaging.eventstreaming.Tag;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.condition.EnabledIfSystemProperty;
import org.springframework.boot.builder.SpringApplicationBuilder;
import org.springframework.context.ConfigurableApplicationContext;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

@EnabledIfSystemProperty(named = "stage.restart", matches = "true")
// Probe is outside default Surefire discovery, so the opt-in proof adds no default skipped tests.
class PersistentRestartProbe {
    public record Recorded(@EventTag(key = "owner") String owner) {}

    @Test
    void viewlessFixturePreservesEventsAndClaimsAcrossTwoBoots() throws Exception {
        survivesTwoBoots(false);
    }

    @Test
    void selectedApplicationRefusesViewsWithoutReadinessOrDataMutation() throws Exception {
        if (!Boolean.getBoolean("stage.expectViews")) {
            // A successful main-app probe must genuinely have no generated views; never filter them.
            survivesTwoBoots(true);
            return;
        }
        var database = PersistentProbeSupport.database(PersistentProbeSupport.freshPath());
        var owner = UUID.randomUUID().toString();
        var checkpoint = "safety-checkpoint-" + UUID.randomUUID();
        try (var seed = PersistentProbeSupport.viewless(database)) {
            assertPersistentStores(seed);
            append(seed, owner).get(10, TimeUnit.SECONDS);
            assertEquals(1, count(seed, owner));
            var tokens = seed.getBean(Configuration.class).getComponent(TokenStore.class);
            assertInstanceOf(JpaTokenStore.class, tokens);
            seed.getBean(Configuration.class).getComponent(UnitOfWorkFactory.class).create().executeWithResult(context ->
                    tokens.initializeTokenSegments(checkpoint, 1, new GlobalSequenceTrackingToken(42), context))
                    .get(10, TimeUnit.SECONDS);
        }
        var before = PersistentDatabaseSnapshot.read(database);
        before.assertSeeded(owner, checkpoint);
        for (var attempt = 1; attempt <= 2; attempt++) {
            if (PersistentProbeSupport.unsupportedViews()) PersistentProbeSupport.refuses(database);
            else PersistentProbeSupport.refusesLegacy(database);
            assertEquals(before, PersistentDatabaseSnapshot.read(database), "All event/claim/token values unchanged after refusal " + attempt);
        }
        var fresh = PersistentProbeSupport.freshPath();
        if (PersistentProbeSupport.unsupportedViews()) {
            PersistentProbeSupport.refuses(PersistentProbeSupport.database(fresh));
            try (var files = Files.list(fresh.getParent())) {
                assertEquals(0, files.count(), "Unsupported-shape refusal must not create any fresh database files");
            }
        } else {
            try (var application = PersistentProbeSupport.start(new SpringApplicationBuilder(PersistentProbeSupport.application()), PersistentProbeSupport.database(fresh))) {
                assertPersistentStores(application);
                assertFalse(application.getBeansOfType(ReadModelStore.class).isEmpty(), "Supported flat application must provide durable views");
            }
            try (var files = Files.list(fresh.getParent())) {
                assertTrue(files.count() > 0, "Supported flat startup must establish generation metadata");
            }
        }
    }

    private void survivesTwoBoots(boolean main) throws Exception {
        var database = PersistentProbeSupport.database(PersistentProbeSupport.freshPath());
        var owner = UUID.randomUUID().toString();
        var competitor = UUID.randomUUID().toString();
        try (var first = boot(database, main)) {
            assertPersistentStores(first);
            append(first, owner).get(10, TimeUnit.SECONDS);
            assertEquals(1, count(first, owner));
        }
        try (var second = boot(database, main)) {
            assertPersistentStores(second);
            assertEquals(1, count(second, owner));
            var failure = assertThrows(Exception.class, () -> append(second, competitor).get(10, TimeUnit.SECONDS));
            assertInstanceOf(UniqueValueConflict.class, root(failure));
            assertEquals(0, count(second, competitor));
            assertEquals(1, count(second, owner));
        }
        System.out.println("Viewless two-boot proof: event and unique claim survived; conflicting owner appended zero events");
    }

    private ConfigurableApplicationContext boot(String database, boolean main) throws Exception {
        return main ? PersistentProbeSupport.start(new SpringApplicationBuilder(PersistentProbeSupport.application()), database)
                : PersistentProbeSupport.viewless(database);
    }

    private void assertPersistentStores(ConfigurableApplicationContext application) {
        assertInstanceOf(AggregateBasedJpaEventStorageEngine.class, application.getBean(EventStorageEngine.class));
        assertInstanceOf(JpaUniqueValueStore.class, application.getBean(UniqueValueStore.class));
        var views = application.getBeansOfType(ReadModelStore.class);
        if (!Boolean.getBoolean("stage.expectViews")) assertTrue(views.isEmpty(), "Viewless fixture must remain viewless");
        views.forEach((name, store) -> assertTrue(store.getClass().getName().endsWith(".persistence.JpaReadModelStore"),
                "Persistent view must not fall back to memory: " + name));
        assertTrue(application.getBeansOfType(PersistentReadModelSafety.class).isEmpty());
    }

    private CompletableFuture<Void> append(ConfigurableApplicationContext application, String owner) {
        return application.getBean(Configuration.class).getComponent(UnitOfWorkFactory.class).create().executeWithResult(context -> {
            application.getBean(UniqueValueStore.class).assign(context, "restart", List.of("claimed"), owner, false, "already claimed");
            EventAppender.forContext(context).append(new Recorded(owner));
            return CompletableFuture.<Void>completedFuture(null);
        });
    }

    private int count(ConfigurableApplicationContext application, String owner) throws Exception {
        var stream = application.getBean(EventStorageEngine.class).source(SourcingCondition.conditionFor(EventCriteria.havingTags(Tag.of("owner", owner))));
        try {
            return stream.filter(entry -> !(entry.message() instanceof TerminalEventMessage))
                    .collect(ArrayList<Object>::new, (all, entry) -> all.add(entry)).get(10, TimeUnit.SECONDS).size();
        } finally { stream.close(); }
    }

    private Throwable root(Throwable failure) {
        while (failure.getCause() != null) failure = failure.getCause();
        return failure;
    }
}
