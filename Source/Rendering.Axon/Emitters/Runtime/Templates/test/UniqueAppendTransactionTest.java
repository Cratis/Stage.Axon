// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

package __RUNTIME__;

import static org.assertj.core.api.Assertions.*;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CyclicBarrier;
import java.util.concurrent.TimeUnit;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.context.annotation.Import;
import org.axonframework.common.configuration.Configuration;
import org.axonframework.eventsourcing.eventstore.AppendCondition;
import org.axonframework.eventsourcing.eventstore.EventStorageEngine;
import org.axonframework.eventsourcing.eventstore.SourcingCondition;
import org.axonframework.messaging.core.unitofwork.ProcessingContext;
import org.axonframework.messaging.core.unitofwork.UnitOfWorkFactory;
import org.axonframework.messaging.eventhandling.GenericEventMessage;
import org.axonframework.messaging.eventhandling.annotation.Event;
import org.axonframework.messaging.eventstreaming.EventCriteria;
import org.axonframework.messaging.eventstreaming.Tag;
import org.axonframework.messaging.core.annotation.AnnotationMessageTypeResolver;

@SpringBootTest
@AutoConfigureMockMvc
@Import(RuntimeHttpRoundTripTest.EchoEndpoint.class)
class UniqueAppendTransactionTest {
    @Autowired Configuration configuration;
    @Autowired UniqueValueStore claims;
    @Autowired EventStorageEngine events;

    @Event(namespace = "stage-tests", name = "UniqueClaimed", version = "1")
    record Claimed(@org.axonframework.eventsourcing.annotation.EventTag(key = "owner") String owner) {}

    @Test
    void rollsBackTheClaimAndAppendTogether() throws Exception {
        var constraint = UUID.randomUUID().toString();
        var failedOwner = UUID.randomUUID().toString();
        assertThatThrownBy(() -> append(constraint, "A", failedOwner, true).join()).hasRootCauseMessage("forced append rollback");
        assertThat(count(failedOwner)).isZero();
        var nextOwner = UUID.randomUUID().toString();
        append(constraint, "a", nextOwner, false).get(10, TimeUnit.SECONDS);
        assertThat(count(nextOwner)).isOne();
        assertThatThrownBy(() -> append(constraint, "A", failedOwner, false).join()).hasRootCauseInstanceOf(UniqueValueConflict.class);
        assertThat(count(failedOwner)).isZero();
    }

    @Test
    void keepsAPreexistingSameOwnerClaimWhenTheNextAppendFails() throws Exception {
        var constraint = UUID.randomUUID().toString();
        var owner = UUID.randomUUID().toString();
        change(context -> claims.assign(context, constraint, List.of("same"), owner, false, "claimed")).join();
        assertThatThrownBy(() -> append(constraint, "same", owner, true).join()).hasRootCauseMessage("forced append rollback");
        assertThatThrownBy(() -> append(constraint, "same", UUID.randomUUID().toString(), false).join()).hasRootCauseInstanceOf(UniqueValueConflict.class);
        assertThat(count(owner)).isZero();
    }

    @Test
    void releasesAndReplacesOnlyTheOwnersClaim() {
        var constraint = UUID.randomUUID().toString();
        change(context -> claims.assign(context, constraint, List.of("old"), "owner", false, "claimed")).join();
        change(context -> claims.assign(context, constraint, List.of("new"), "owner", false, "claimed")).join();
        change(context -> claims.assign(context, constraint, List.of("old"), "other", false, "claimed")).join();
        change(context -> claims.releaseOwner(context, constraint, "owner")).join();
        change(context -> claims.assign(context, constraint, List.of("new"), "third", false, "claimed")).join();
        assertThatThrownBy(() -> change(context -> claims.assign(context, constraint, List.of("old"), "third", false, "claimed")).join()).hasRootCauseInstanceOf(UniqueValueConflict.class);
    }

    @Test
    void permitsOnlyOneConcurrentOwnerAndOneEvent() throws Exception {
        var constraint = UUID.randomUUID().toString();
        var first = UUID.randomUUID().toString();
        var second = UUID.randomUUID().toString();
        var barrier = new CyclicBarrier(2);
        var a = CompletableFuture.supplyAsync(() -> race(barrier, constraint, first));
        var b = CompletableFuture.supplyAsync(() -> race(barrier, constraint, second));
        assertThat(List.of(a.get(15, TimeUnit.SECONDS), b.get(15, TimeUnit.SECONDS))).containsExactlyInAnyOrder(true, false);
        assertThat(count(first) + count(second)).isOne();
    }

    @Test
    void mapsAConflictToProblemDetail409() {
        var problem = new ProblemDetailsAdvice().conflict(new UniqueValueConflict("already claimed"));
        assertThat(problem.getStatus()).isEqualTo(409);
        assertThat(problem.getDetail()).isEqualTo("already claimed");
    }

    private boolean race(CyclicBarrier barrier, String constraint, String owner) {
        try { barrier.await(5, TimeUnit.SECONDS); append(constraint, "race", owner, false).get(10, TimeUnit.SECONDS); return true; }
        catch (Exception failure) { assertThat(root(failure)).isInstanceOf(UniqueValueConflict.class); return false; }
    }

    private CompletableFuture<Void> change(java.util.function.Consumer<ProcessingContext> operation) {
        return configuration.getComponent(UnitOfWorkFactory.class).create().executeWithResult(context -> {
            operation.accept(context);
            return CompletableFuture.completedFuture(null);
        });
    }

    private CompletableFuture<Void> append(String constraint, String value, String owner, boolean fail) {
        return configuration.getComponent(UnitOfWorkFactory.class).create().executeWithResult(context -> {
            claims.assign(context, constraint, List.of(value), owner, true, "already claimed");
            org.axonframework.messaging.eventhandling.gateway.EventAppender.forContext(context).append(new Claimed(owner));
            if (fail) throw new IllegalStateException("forced append rollback");
            return CompletableFuture.completedFuture(null);
        });
    }

    private long count(String owner) throws Exception {
        var stream = events.source(SourcingCondition.conditionFor(EventCriteria.havingTags(Tag.of("owner", owner))));
        try { return stream.filter(entry -> !(entry.message() instanceof org.axonframework.eventsourcing.eventstore.TerminalEventMessage)).collect(java.util.ArrayList<Object>::new, (all, entry) -> all.add(entry)).get(5, TimeUnit.SECONDS).size(); }
        finally { stream.close(); }
    }

    private static Throwable root(Throwable failure) {
        while (failure.getCause() != null) failure = failure.getCause();
        return failure;
    }
}
