package __APPLICATION__.runtime;

import __APPLICATION__.accounts.aliases.requestassignment.RequestAlias;
import __APPLICATION__.accounts.aliases.requestrelease.RequestRelease;
import __APPLICATION__.accounts.events.AliasRequested;
import __APPLICATION__.accounts.events.ReleaseRequested;
import __APPLICATION__.concepts.OwnerId;
import java.time.Duration;
import java.util.UUID;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.Timeout;
import static org.awaitility.Awaitility.await;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

@Timeout(30)
abstract class GeneratedReactionConstraintTest extends GeneratedReactionProofSupport {
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
    void releasedByReactionFreesTheValueForAnotherOwner() throws Exception {
        var owner = UUID.randomUUID().toString();
        var next = UUID.randomUUID().toString();
        var value = UUID.randomUUID().toString();
        assign(owner, value);
        assertConflict(next, value);
        gateway.sendAndWait(new RequestRelease(new OwnerId(owner), value));
        await().atMost(Duration.ofSeconds(10)).untilAsserted(() -> assertEquals(2, events(owner)));
        assign(next, value);
        assertEquals(1, events(next));
        assertConflict(UUID.randomUUID().toString(), value);
    }

    @Test
    void replaySkipsBothAssignmentAndReleaseWithoutReapplyingConstraints() throws Exception {
        var owner = new OwnerId(UUID.randomUUID().toString());
        var value = UUID.randomUUID().toString();
        var requested = new AliasRequested(owner, value);
        deliver(assignReaction, requested, false);
        assertEquals(1, events(owner.value()), "Regular annotated delivery must execute the reaction");
        deliver(assignReaction, requested, true);
        deliver(releaseReaction, new ReleaseRequested(owner, value), true);
        assertEquals(1, events(owner.value()), "Replay must neither append another assignment nor release");
        assertConflict(UUID.randomUUID().toString(), value);
    }

    private void assertConflict(String owner, String value) {
        var failure = assertThrows(Exception.class, () -> assign(owner, value));
        assertTrue(violation(root(failure)), () -> failure.toString());
    }
}
