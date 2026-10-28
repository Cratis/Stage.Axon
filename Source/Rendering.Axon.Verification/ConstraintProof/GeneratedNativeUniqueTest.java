package __RUNTIME__;

import __APPLICATION__.accounts.aliases.claim.ClaimAlias;
import __APPLICATION__.accounts.aliases.failingclaim.ClaimAliasThenFail;
import __APPLICATION__.accounts.aliases.quietclaim.ClaimAliasQuietly;
import __APPLICATION__.accounts.aliases.quietrelease.ReleaseAliasQuietly;
import __APPLICATION__.accounts.aliases.release.ReleaseAlias;
import __APPLICATION__.accounts.events.AliasClaimed;
import __APPLICATION__.accounts.events.AccountOpened;
import __APPLICATION__.accounts.events.EventTags;
import __APPLICATION__.accounts.openings.close.CloseAccount;
import __APPLICATION__.accounts.openings.failingopen.OpenAccountThenFail;
import __APPLICATION__.accounts.openings.modeledopen.OpenAccountModeled;
import __APPLICATION__.accounts.openings.open_.OpenAccount;
import __APPLICATION__.concepts.AccountId;
import __RUNTIME_MODULE__.UniqueValueConflict;
import java.util.ArrayList;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.ConcurrentLinkedQueue;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import org.axonframework.common.configuration.Configuration;
import org.axonframework.eventsourcing.eventstore.EventStorageEngine;
import org.axonframework.eventsourcing.eventstore.SourcingCondition;
import org.axonframework.eventsourcing.eventstore.TerminalEventMessage;
import org.axonframework.messaging.commandhandling.gateway.CommandGateway;
import org.axonframework.messaging.eventhandling.EventMessage;
import org.axonframework.messaging.eventhandling.conversion.EventConverter;
import org.axonframework.messaging.eventstreaming.EventCriteria;
import org.axonframework.messaging.eventstreaming.Tag;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.test.annotation.DirtiesContext;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

@SpringBootTest(webEnvironment = SpringBootTest.WebEnvironment.NONE)
@DirtiesContext(classMode = DirtiesContext.ClassMode.AFTER_CLASS)
__BASE_MODIFIER__class GeneratedNativeUniqueTest {
    private static final int CONTENDERS = 8;
    private static final int REPETITIONS = 25;
    private final ExecutorService workers = Executors.newFixedThreadPool(CONTENDERS);
    @Autowired private CommandGateway gateway;
    @Autowired private Configuration configuration;

    @AfterEach
    void stopContenders() throws Exception {
        workers.shutdownNow();
        assertTrue(workers.awaitTermination(10, TimeUnit.SECONDS), "All contenders must stop");
    }

    @Test
    void exactlyOneConcurrentClaimWins() throws Exception {
        for (var repetition = 0; repetition < REPETITIONS; repetition++) {
            var alias = unique();
            var ready = new CountDownLatch(CONTENDERS);
            var start = new CountDownLatch(1);
            var owners = new ArrayList<AccountId>();
            var failures = new ConcurrentLinkedQueue<String>();
            var calls = new ArrayList<CompletableFuture<Boolean>>();
            for (var index = 0; index < CONTENDERS; index++) {
                var owner = new AccountId(unique());
                owners.add(owner);
                calls.add(CompletableFuture.supplyAsync(() -> {
                    ready.countDown();
                    await(start);
                    try { claim(owner, alias); return true; }
                    catch (Exception failure) { return lost(failure, failures); }
                }, workers));
            }
            assertTrue(ready.await(5, TimeUnit.SECONDS), "All contenders must arrive");
            start.countDown();
            var winners = 0;
            for (var call : calls) if (call.get(10, TimeUnit.SECONDS)) winners++;
            assertEquals(1, winners, () -> "Contender failures: " + failures);
            var events = 0L;
            for (var index = 0; index < owners.size(); index++) {
                var facts = facts(owners.get(index));
                assertEquals(calls.get(index).get(10, TimeUnit.SECONDS) ? 1 : 0, facts.size());
                for (var event : facts) assertEquals(alias, payload(event, AliasClaimed.class).alias());
                events += facts.size();
            }
            assertEquals(1, events);
        }
    }

    @Test
    void changingTheValueReleasesThePrevious() throws Exception {
        var owner = new AccountId(unique());
        var other = new AccountId(unique());
        var first = unique();
        var second = unique();
        claim(owner, first);
        claim(owner, second);
        claim(other, first);
        conflict(new ClaimAlias(other, second));
        assertEquals(2, facts(owner).size());
        assertEquals(1, facts(other).size());
    }

    @Test
    void releaseFreesTheValue() throws Exception {
        var owner = new AccountId(unique());
        var other = new AccountId(unique());
        var alias = unique();
        claim(owner, alias);
        send(new ReleaseAlias(owner));
        claim(other, alias);
        assertEquals(2, facts(owner).size());
        assertEquals(1, facts(other).size());
    }

    @Test
    void rolledBackBodyKeepsNoReservation() throws Exception {
        var owner = new AccountId(unique());
        var other = new AccountId(unique());
        var alias = unique();
        var failure = assertThrows(Exception.class, () -> send(new ClaimAliasThenFail(owner, alias)));
        assertInstanceOf(IllegalStateException.class, root(failure));
        assertEquals("forced rollback", root(failure).getMessage());
        assertEquals(0, facts(owner).size());
        claim(other, alias);
        assertEquals(1, facts(other).size());
    }

    @Test
    void swallowedConflictStillFails() throws Exception {
        var owner = new AccountId(unique());
        var other = new AccountId(unique());
        var alias = unique();
        claim(owner, alias);
        conflict(new ClaimAliasQuietly(other, alias));
        assertEquals(0, facts(other).size());
        assertEquals(1, facts(owner).size());
        send(new ReleaseAlias(owner));
        claim(other, alias);
        assertEquals(1, facts(other).size());
    }

    @Test
    void swallowedDelegateFailureKeepsTheOriginalClaim() throws Exception {
        var owner = new AccountId(unique());
        var other = new AccountId(unique());
        var alias = unique();
        claim(owner, alias);
        assertThrows(Exception.class, () -> send(new ReleaseAliasQuietly(owner)));
        assertEquals(1, facts(owner).size());
        conflict(new ClaimAlias(other, alias));
        assertEquals(0, facts(other).size());
        send(new ReleaseAlias(owner));
        claim(other, alias);
        assertEquals(1, facts(other).size());
    }

    @Test
    void exactlyOneConcurrentOpeningWins() throws Exception {
        for (var repetition = 0; repetition < REPETITIONS; repetition++) {
            var owner = new AccountId(unique());
            var ready = new CountDownLatch(CONTENDERS);
            var start = new CountDownLatch(1);
            var failures = new ConcurrentLinkedQueue<String>();
            var calls = new ArrayList<CompletableFuture<Boolean>>();
            for (var index = 0; index < CONTENDERS; index++) {
                Object command = index % 2 == 0 ? new OpenAccount(owner) : new OpenAccountModeled(owner);
                calls.add(CompletableFuture.supplyAsync(() -> {
                    ready.countDown();
                    await(start);
                    try { send(command); return true; }
                    catch (Exception failure) { return lost(failure, failures); }
                }, workers));
            }
            assertTrue(ready.await(5, TimeUnit.SECONDS), "All contenders must arrive");
            start.countDown();
            var winners = 0;
            for (var call : calls) if (call.get(10, TimeUnit.SECONDS)) winners++;
            assertEquals(1, winners, () -> "Contender failures: " + failures);
            var facts = facts(owner);
            assertEquals(1, facts.size());
            assertEquals(owner, payload(facts.getFirst(), AccountOpened.class).accountId());
        }
    }

    @Test
    void nativeOpeningBlocksModeledOpening() throws Exception {
        var owner = new AccountId(unique());
        send(new OpenAccount(owner));
        conflict(new OpenAccountModeled(owner));
        assertEquals(1, facts(owner).size());
    }

    @Test
    void modeledOpeningBlocksNativeOpening() throws Exception {
        var owner = new AccountId(unique());
        send(new OpenAccountModeled(owner));
        conflict(new OpenAccount(owner));
        assertEquals(1, facts(owner).size());
    }

    @Test
    void closingReleasesTheOpening() throws Exception {
        var owner = new AccountId(unique());
        send(new OpenAccount(owner));
        send(new CloseAccount(owner));
        send(new OpenAccountModeled(owner));
        assertEquals(3, facts(owner).size());
        conflict(new OpenAccount(owner));
    }

    @Test
    void rolledBackOpeningKeepsNoReservation() throws Exception {
        var owner = new AccountId(unique());
        var failure = assertThrows(Exception.class, () -> send(new OpenAccountThenFail(owner)));
        assertInstanceOf(IllegalStateException.class, root(failure));
        assertEquals("forced rollback", root(failure).getMessage());
        assertEquals(0, facts(owner).size());
        send(new OpenAccountModeled(owner));
        assertEquals(1, facts(owner).size());
    }

    private void claim(AccountId owner, String alias) throws Exception {
        send(new ClaimAlias(owner, alias));
    }

    private void send(Object command) throws Exception {
        gateway.send(command, Void.class).get(10, TimeUnit.SECONDS);
    }

    private void conflict(Object command) {
        var failure = assertThrows(Exception.class, () -> send(command));
        assertInstanceOf(UniqueValueConflict.class, root(failure));
    }

    private List<EventMessage> facts(AccountId owner) throws Exception {
        var stream = configuration.getComponent(EventStorageEngine.class).source(SourcingCondition.conditionFor(
                EventCriteria.havingTags(Tag.of(EventTags.ACCOUNT_ID, owner.toString()))));
        try {
            return stream.filter(entry -> !(entry.message() instanceof TerminalEventMessage))
                    .collect(ArrayList<EventMessage>::new, (all, entry) -> all.add(entry)).get(10, TimeUnit.SECONDS);
        } finally { stream.close(); }
    }

    private <T> T payload(EventMessage event, Class<T> type) {
        return event.payloadAs(type, configuration.getComponent(EventConverter.class));
    }

    private static boolean lost(Throwable failure, ConcurrentLinkedQueue<String> failures) {
        var cause = root(failure);
        failures.add(cause.getClass().getName() + ": " + cause.getMessage());
        assertInstanceOf(UniqueValueConflict.class, cause, failure::toString);
        return false;
    }

    private static void await(CountDownLatch latch) {
        try { assertTrue(latch.await(5, TimeUnit.SECONDS), "All contenders must start"); }
        catch (InterruptedException failure) {
            Thread.currentThread().interrupt();
            throw new AssertionError(failure);
        }
    }

    private static String unique() { return UUID.randomUUID().toString(); }

    private static Throwable root(Throwable failure) {
        while (failure.getCause() != null) failure = failure.getCause();
        return failure;
    }
}
