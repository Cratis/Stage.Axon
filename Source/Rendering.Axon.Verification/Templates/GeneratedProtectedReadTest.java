package __RUNTIME__;

import __APPLICATION__.accounts.registry.consume.ConsumeAccount;
import __APPLICATION__.accounts.registry.crossstream.CloseOtherAccount;
import __APPLICATION__.concepts.AccountId;
import java.sql.SQLIntegrityConstraintViolationException;
import java.util.ArrayList;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ConcurrentSkipListSet;
import java.util.concurrent.CyclicBarrier;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicReference;
import org.axonframework.common.configuration.Configuration;
import org.axonframework.eventsourcing.eventstore.AppendEventsTransactionRejectedException;
import org.axonframework.messaging.commandhandling.CommandMessage;
import org.axonframework.messaging.commandhandling.gateway.CommandGateway;
import org.axonframework.messaging.core.MessageHandlerInterceptor;
import org.axonframework.modelling.ConcurrencyException;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.Timeout;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Import;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

@Import(GeneratedProtectedReadTest.Seam.class)
@Timeout(60)
abstract class GeneratedProtectedReadTest {
    @Autowired private CommandGateway gateway;
    @Autowired private Configuration configuration;
    private static final AtomicReference<CyclicBarrier> BARRIER = new AtomicReference<>();

    @TestConfiguration
    static class Seam {
        @Bean
        MessageHandlerInterceptor<CommandMessage> holdBetweenReadAndAppend() {
            return (message, context, chain) -> {
                var barrier = BARRIER.get();
                if (barrier != null && message.payload() instanceof ConsumeAccount) {
                    context.runOnPostInvocation(invoked -> {
                        try {
                            barrier.await(5, TimeUnit.SECONDS);
                        } catch (Exception failure) {
                            throw new IllegalStateException("contenders did not interleave", failure);
                        }
                    });
                }
                return chain.proceed(message, context);
            };
        }
    }

    @Test
    void generatedProtectedDecisionsCommitOneConsumer() throws Exception {
        var outcomes = new ConcurrentSkipListSet<String>();
        for (var repetition = 0; repetition < 25; repetition++) {
            var id = UUID.randomUUID();
            GeneratedIngressSupport.register(gateway, id, UUID.randomUUID().toString()).get(10, TimeUnit.SECONDS);
            assertEquals(1, GeneratedIngressSupport.facts(configuration, id).size(), "The protected read must be seeded");
            BARRIER.set(new CyclicBarrier(8));
            var workers = Executors.newFixedThreadPool(8);
            try {
                var calls = new ArrayList<CompletableFuture<Boolean>>();
                for (var index = 0; index < 8; index++) {
                    calls.add(CompletableFuture.supplyAsync(() -> {
                        try {
                            GeneratedIngressSupport.send(gateway, new ConsumeAccount(new AccountId(id), "consumed")).get(10, TimeUnit.SECONDS);
                            return true;
                        } catch (Exception failure) {
                            var actual = GeneratedIngressSupport.root(failure);
                            outcomes.add(actual.getClass().getName());
                            assertTrue(actual instanceof ConcurrencyException
                                    || actual instanceof AppendEventsTransactionRejectedException
                                    || actual instanceof IllegalArgumentException
                                    || actual instanceof SQLIntegrityConstraintViolationException,
                                    () -> "Unexpected protected-decision outcome: " + actual);
                            return false;
                        }
                    }, workers));
                }
                var winners = 0;
                for (var call : calls) if (call.get(10, TimeUnit.SECONDS)) winners++;
                assertEquals(1, winners);
                assertEquals(2, GeneratedIngressSupport.facts(configuration, id).size(), "Only the seeded fact and the winner may persist");
            } finally {
                workers.shutdownNow();
                assertTrue(workers.awaitTermination(5, TimeUnit.SECONDS), "Every protected-read contender must terminate");
                BARRIER.set(null);
            }
        }
        assertTrue(!outcomes.isEmpty(), "The proof must observe losing decisions");
        System.out.println("Protected decision outcomes (" + getClass().getSimpleName() + "): " + outcomes);
    }

    @Test
    void unsupportedCrossStreamReadFailsBeforeAppend() throws Exception {
        var owner = UUID.randomUUID();
        var other = UUID.randomUUID();
        GeneratedIngressSupport.register(gateway, owner, UUID.randomUUID().toString()).get(10, TimeUnit.SECONDS);
        GeneratedIngressSupport.register(gateway, other, UUID.randomUUID().toString()).get(10, TimeUnit.SECONDS);
        var failure = assertThrows(Exception.class, () -> GeneratedIngressSupport.send(gateway,
                new CloseOtherAccount(new AccountId(owner), new AccountId(other), "must not append")).get(10, TimeUnit.SECONDS));
        var actual = assertInstanceOf(UnsupportedOperationException.class, GeneratedIngressSupport.root(failure));
        assertTrue(actual.getMessage().contains("STAGE-AXON-0106"));
        assertEquals(1, GeneratedIngressSupport.facts(configuration, owner).size());
        assertEquals(1, GeneratedIngressSupport.facts(configuration, other).size());
    }

    @Test
    void absentRequiredReadFailsBeforeAppend() throws Exception {
        var owner = UUID.randomUUID();
        var failure = assertThrows(Exception.class, () -> GeneratedIngressSupport.send(gateway,
                new ConsumeAccount(new AccountId(owner), "must not append")).get(10, TimeUnit.SECONDS));
        assertInstanceOf(IllegalArgumentException.class, GeneratedIngressSupport.root(failure));
        assertEquals(0, GeneratedIngressSupport.facts(configuration, owner).size());
    }
}
