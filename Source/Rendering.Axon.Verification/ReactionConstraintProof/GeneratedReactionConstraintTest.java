package __APPLICATION__.runtime;

import __APPLICATION__.accounts.aliases.requestassignment.RequestAlias;
import __APPLICATION__.accounts.aliases.requestrelease.RequestRelease;
import __APPLICATION__.accounts.events.AliasRequested;
import __APPLICATION__.accounts.events.ReleaseRequested;
import __APPLICATION__.concepts.OwnerId;
import io.cratis.stage.axon.runtime.UniqueValueConflict;
import java.time.Duration;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.TimeUnit;
import org.axonframework.messaging.core.unitofwork.UnitOfWorkFactory;
import org.axonframework.messaging.eventhandling.gateway.EventAppender;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.Timeout;
import static org.awaitility.Awaitility.await;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertSame;
import static org.junit.jupiter.api.Assertions.assertThrows;

@Timeout(30)
abstract class GeneratedReactionConstraintTest extends GeneratedReactionProofSupport {
    protected abstract Class<?> storeType();

    @Test
    void springSelectsTheAutowiredConstructorAndSharesTheConfiguredStore() throws Exception {
        assertInstanceOf(storeType(), claims);
        assertSame(claims, injectedStore(assignment));
        assertSame(claims, injectedStore(release));
    }

    @Test
    void liveReactionClaimRefusesAConflictingAppendFromAnotherOwner() throws Exception {
        var owner = UUID.randomUUID().toString();
        var loser = UUID.randomUUID().toString();
        var value = UUID.randomUUID().toString();
        gateway.sendAndWait(new RequestAlias(new OwnerId(owner), value));
        await().atMost(Duration.ofSeconds(10)).untilAsserted(() -> assertEquals(1, events(owner)));
        assertConflict(loser, value);
        assertEquals(0, events(loser), "Rejected reaction append must not write an event");
    }

    @Test
    void rollbackAfterTheGeneratedHandlerAppendsLeavesNeitherEventNorReservation() throws Exception {
        var owner = UUID.randomUUID().toString();
        var next = UUID.randomUUID().toString();
        var value = UUID.randomUUID().toString();
        var failure = assertThrows(Exception.class, () -> configuration.getComponent(UnitOfWorkFactory.class).create()
                .executeWithResult(context -> {
                    assignment.handle(assignment(owner, value), EventAppender.forContext(context), context, null);
                    throw new IllegalStateException("forced reaction append rollback");
                }).get(10, TimeUnit.SECONDS));
        assertEquals("forced reaction append rollback", root(failure).getMessage());
        assertEquals(0, events(owner), "Rolled-back generated append must not persist an event");
        assign(next, value);
        assertEquals(1, events(next), "A different owner must be able to reuse the rolled-back claim");
    }

    @Test
    void releasedByReactionFreesTheValueForAnotherOwner() throws Exception {
        var owner = UUID.randomUUID().toString();
        var next = UUID.randomUUID().toString();
        var value = UUID.randomUUID().toString();
        assign(owner, value);
        assertConflict(next, value);
        gateway.sendAndWait(new RequestRelease(new OwnerId(owner)));
        await().atMost(Duration.ofSeconds(10)).untilAsserted(() -> assertEquals(2, events(owner)));
        assign(next, value);
        assertEquals(1, events(next));
        assertConflict(UUID.randomUUID().toString(), value);
    }

    @Test
    void rollbackOfReleasedByKeepsTheCommittedClaim() throws Exception {
        var owner = UUID.randomUUID().toString();
        var value = UUID.randomUUID().toString();
        assign(owner, value);
        var failure = assertThrows(Exception.class, () -> configuration.getComponent(UnitOfWorkFactory.class).create()
                .executeWithResult(context -> {
                    release.handle(release(owner), EventAppender.forContext(context), context, null);
                    return CompletableFuture.failedFuture(new IllegalStateException("forced release rollback"));
                }).get(10, TimeUnit.SECONDS));
        assertEquals("forced release rollback", root(failure).getMessage());
        assertEquals(1, events(owner));
        assertConflict(UUID.randomUUID().toString(), value);
        gateway.sendAndWait(release(owner));
        assign(UUID.randomUUID().toString(), value);
    }

    @Test
    void replaySkipsBothAssignmentAndReleaseWithoutReapplyingConstraints() throws Exception {
        var owner = new OwnerId(UUID.randomUUID().toString());
        var value = UUID.randomUUID().toString();
        var requested = new AliasRequested(owner, value);
        deliver(assignReaction, requested, false);
        assertEquals(1, events(owner.value()), "Regular annotated delivery must execute the reaction");
        deliver(assignReaction, requested, true);
        deliver(releaseReaction, new ReleaseRequested(owner), true);
        assertEquals(1, events(owner.value()), "Replay must neither append another assignment nor release");
        assertConflict(UUID.randomUUID().toString(), value);
    }

    private void assertConflict(String owner, String value) {
        assertInstanceOf(UniqueValueConflict.class, root(assertThrows(Exception.class, () -> assign(owner, value))));
    }
}
