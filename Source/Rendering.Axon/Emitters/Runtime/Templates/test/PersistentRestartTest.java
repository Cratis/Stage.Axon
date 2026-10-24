// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

package __RUNTIME__;

import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.TimeUnit;
import org.axonframework.common.configuration.Configuration;
import org.axonframework.eventsourcing.eventstore.EventStorageEngine;
import org.axonframework.eventsourcing.eventstore.SourcingCondition;
import org.axonframework.eventsourcing.eventstore.jpa.AggregateBasedJpaEventStorageEngine;
import org.axonframework.messaging.core.unitofwork.UnitOfWorkFactory;
import org.axonframework.messaging.eventhandling.gateway.EventAppender;
import org.axonframework.messaging.eventstreaming.EventCriteria;
import org.axonframework.messaging.eventstreaming.Tag;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.condition.EnabledIfSystemProperty;
import org.springframework.boot.builder.SpringApplicationBuilder;
import org.springframework.context.ConfigurableApplicationContext;
import static org.junit.jupiter.api.Assertions.*;

@EnabledIfSystemProperty(named = "stage.restart", matches = "true")
class PersistentRestartTest {
    public record Recorded(@org.axonframework.eventsourcing.annotation.EventTag(key = "owner") String owner) {}

    @Test
    void eventsAndClaimsSurviveTwoApplicationBootsOnTheSameFile() throws Exception {
        var path = Path.of("target", "persistent-restart", UUID.randomUUID().toString(), "events").toAbsolutePath();
        Files.createDirectories(path.getParent());
        var database = "jdbc:h2:file:" + path;
        var owner = UUID.randomUUID().toString();
        var competitor = UUID.randomUUID().toString();
        try (var first = boot(database)) {
            assertPersistentStores(first);
            append(first, owner).get(10, TimeUnit.SECONDS);
            assertEquals(1, count(first, owner));
        }
        try (var second = boot(database)) {
            assertPersistentStores(second);
            assertEquals(1, count(second, owner));
            var failure = assertThrows(Exception.class, () -> append(second, competitor).get(10, TimeUnit.SECONDS));
            assertInstanceOf(UniqueValueConflict.class, root(failure));
            assertEquals(0, count(second, competitor));
            assertEquals(1, count(second, owner));
        }
    }

    private ConfigurableApplicationContext boot(String database) throws Exception {
        var application = Class.forName(getClass().getPackageName().replaceFirst("\\.runtime$", ".Application"));
        return new SpringApplicationBuilder(application).profiles("persistent").run(
                "--server.port=0", "--spring.datasource.url=" + database,
                "--spring.jpa.hibernate.ddl-auto=update", "--spring.main.banner-mode=off");
    }

    private void assertPersistentStores(ConfigurableApplicationContext application) {
        assertInstanceOf(AggregateBasedJpaEventStorageEngine.class, application.getBean(EventStorageEngine.class));
        assertInstanceOf(__RUNTIME__.persistent.JpaUniqueValueStore.class, application.getBean(UniqueValueStore.class));
    }

    private java.util.concurrent.CompletableFuture<Void> append(ConfigurableApplicationContext application, String owner) {
        return application.getBean(Configuration.class).getComponent(UnitOfWorkFactory.class).create().executeWithResult(context -> {
            application.getBean(UniqueValueStore.class).assign(context, "restart", List.of("claimed"), owner, false, "already claimed");
            EventAppender.forContext(context).append(new Recorded(owner));
            return java.util.concurrent.CompletableFuture.<Void>completedFuture(null);
        });
    }

    private int count(ConfigurableApplicationContext application, String owner) throws Exception {
        var stream = application.getBean(EventStorageEngine.class).source(SourcingCondition.conditionFor(EventCriteria.havingTags(Tag.of("owner", owner))));
        try {
            return stream.filter(entry -> !(entry.message() instanceof org.axonframework.eventsourcing.eventstore.TerminalEventMessage))
                    .collect(java.util.ArrayList<Object>::new, (all, entry) -> all.add(entry)).get(10, TimeUnit.SECONDS).size();
        } finally { stream.close(); }
    }

    private Throwable root(Throwable failure) {
        while (failure.getCause() != null) failure = failure.getCause();
        return failure;
    }
}
