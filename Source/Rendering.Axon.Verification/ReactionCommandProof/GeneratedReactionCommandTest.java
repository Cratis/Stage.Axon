package __APPLICATION__.runtime;

import static org.awaitility.Awaitility.await;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;

import __APPLICATION__.bodies.counters.acknowledgecounts.AcknowledgeRecordedCountReaction;
import __APPLICATION__.bodies.counters.record_.RecordCount;
import __APPLICATION__.bodies.events.CountAcknowledged;
import __APPLICATION__.bodies.events.CountRecorded;
import __APPLICATION__.concepts.CounterId;
import java.time.Duration;
import java.util.ArrayList;
import java.util.UUID;
import java.util.concurrent.TimeUnit;
import org.axonframework.common.configuration.Configuration;
import org.axonframework.eventsourcing.eventstore.EventStorageEngine;
import org.axonframework.eventsourcing.eventstore.SourcingCondition;
import org.axonframework.eventsourcing.eventstore.TerminalEventMessage;
import org.axonframework.messaging.commandhandling.gateway.CommandGateway;
import org.axonframework.messaging.core.MessageTypeResolver;
import org.axonframework.messaging.core.annotation.HandlerDefinition;
import org.axonframework.messaging.core.annotation.ParameterResolverFactory;
import org.axonframework.messaging.core.unitofwork.UnitOfWorkFactory;
import org.axonframework.messaging.eventhandling.GenericEventMessage;
import org.axonframework.messaging.eventhandling.annotation.AnnotatedEventHandlingComponent;
import org.axonframework.messaging.eventhandling.conversion.EventConverter;
import org.axonframework.messaging.eventstreaming.EventCriteria;
import org.axonframework.messaging.eventhandling.gateway.EventGateway;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.ObjectProvider;
import org.springframework.beans.factory.SmartInitializingSingleton;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.context.annotation.Bean;

@SpringBootTest
class GeneratedReactionCommandTest {
    private static final String HISTORY = "history-before-the-processor-started";

    // Runs after every bean exists and before the Axon processors start, so the event is genuine history.
    @TestConfiguration
    static class PublishHistoryBeforeProcessorsStart {
        @Bean
        SmartInitializingSingleton history(ObjectProvider<Configuration> configuration) {
            return () -> configuration.getObject().getComponent(EventGateway.class)
                    .publish(java.util.List.of(new CountRecorded(new CounterId(HISTORY), new CounterId(HISTORY), 3L))).join();
        }
    }

    @Autowired private Configuration configuration;
    @Autowired private CommandGateway gateway;
    @Autowired private AcknowledgeRecordedCountReaction reaction;

    @Test
    void anEventLeadsToTheCommandBeingHandledByTheLiveProcessor() throws Exception {
        var counter = UUID.randomUUID().toString();
        gateway.sendAndWait(new RecordCount(new CounterId(counter), 7L));
        await().atMost(Duration.ofSeconds(10)).untilAsserted(() -> assertEquals(1, acknowledged(counter)));
    }

    @Test
    void anEventFromBeforeTheProcessorStartedIsNotReactedTo() throws Exception {
        assertEquals(1, recorded(HISTORY), "The history event must really be in the store");
        var live = UUID.randomUUID().toString();
        gateway.sendAndWait(new RecordCount(new CounterId(live), 7L));
        await().atMost(Duration.ofSeconds(10)).untilAsserted(() -> assertEquals(1, acknowledged(live)));
        assertEquals(0, acknowledged(HISTORY), "A new reaction processor starts at the latest token, not at history");
    }

    @Test
    void aCommandRejectionPropagatesOutOfTheEventHandler() throws Exception {
        var counter = UUID.randomUUID().toString();
        var failure = assertThrows(Exception.class, () -> deliver(new CountRecorded(new CounterId(counter), new CounterId(counter), -1L)));
        assertEquals("A negative count cannot be acknowledged", root(failure).getMessage());
        assertEquals(0, acknowledged(counter), "A rejected command must not append an acknowledgement");
    }

    private void deliver(Object payload) throws Exception {
        var types = configuration.getComponent(MessageTypeResolver.class);
        var component = new AnnotatedEventHandlingComponent<>(reaction,
                configuration.getComponent(ParameterResolverFactory.class),
                configuration.getComponent(HandlerDefinition.class), types,
                configuration.getComponent(EventConverter.class));
        var message = new GenericEventMessage(types.resolveOrThrow(payload), payload);
        configuration.getComponent(UnitOfWorkFactory.class).create()
                .executeWithResult(context -> component.handle(message, context).asCompletableFuture())
                .get(10, TimeUnit.SECONDS);
    }

    private long acknowledged(String counter) throws Exception {
        var stream = configuration.getComponent(EventStorageEngine.class).source(SourcingCondition.conditionFor(EventCriteria.havingAnyTag()));
        try {
            return stream.filter(entry -> !(entry.message() instanceof TerminalEventMessage))
                    .filter(entry -> entry.message().payload() instanceof CountAcknowledged event && event.id().value().equals(counter))
                    .collect(ArrayList<Object>::new, (all, entry) -> all.add(entry)).get(5, TimeUnit.SECONDS).size();
        } finally { stream.close(); }
    }

    private long recorded(String counter) throws Exception {
        var stream = configuration.getComponent(EventStorageEngine.class).source(SourcingCondition.conditionFor(EventCriteria.havingAnyTag()));
        try {
            return stream.filter(entry -> !(entry.message() instanceof TerminalEventMessage))
                    .filter(entry -> entry.message().payload() instanceof CountRecorded event && event.id().value().equals(counter))
                    .collect(ArrayList<Object>::new, (all, entry) -> all.add(entry)).get(5, TimeUnit.SECONDS).size();
        } finally { stream.close(); }
    }

    private Throwable root(Throwable failure) {
        while (failure.getCause() != null) failure = failure.getCause();
        return failure;
    }
}
