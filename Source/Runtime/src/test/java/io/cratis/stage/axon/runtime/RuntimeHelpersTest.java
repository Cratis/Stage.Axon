package io.cratis.stage.axon.runtime;

import io.cratis.fundamentals.concepts.ConceptAs;
import io.cratis.fundamentals.concepts.Generatable;
import io.cratis.fundamentals.concepts.GenerateValue;
import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.io.ObjectInputStream;
import java.io.ObjectOutputStream;
import java.util.List;
import java.util.Set;
import java.util.UUID;
import org.axonframework.eventsourcing.annotation.EventTag;
import org.axonframework.eventsourcing.eventstore.AnnotationBasedTagResolver;
import org.axonframework.messaging.core.annotation.AnnotationMessageTypeResolver;
import org.axonframework.messaging.eventhandling.GenericEventMessage;
import org.axonframework.messaging.eventhandling.annotation.Event;
import org.axonframework.messaging.eventstreaming.Tag;
import org.junit.jupiter.api.Test;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.authority.SimpleGrantedAuthority;
import org.springframework.security.core.context.SecurityContextHolder;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

class RuntimeHelpersTest {
    public record Id(UUID value) implements ConceptAs<UUID>, Generatable<Id> {
        public static Id create() { return new Id(GenerateValue.uuid()); }
        @Override
        public String toString() { return value.toString(); }
    }
    @Event(namespace = "runtime", name = "concept-tag", version = "1")
    public record ConceptTagged(@EventTag(key = "id") Id id) {}
    @Event(namespace = "runtime", name = "fallback-tag", version = "1")
    public record FallbackTagged(@EventTag(key = "eventSourceId") EventSourceId eventSourceId) {}

    @Test
    void primaryTagsUsePrimitiveIdentityValuesNotRecordDebugStrings() {
        var value = "00000000-0000-0000-0000-000000000001";
        assertTag(new ConceptTagged(new Id(UUID.fromString(value))), "id", value);
        assertTag(new FallbackTagged(new EventSourceId(value)), "eventSourceId", value);
    }

    private static void assertTag(Object event, String key, String value) {
        var type = new AnnotationMessageTypeResolver().resolve(event.getClass()).orElseThrow();
        var message = new GenericEventMessage(type, event);
        var tags = new AnnotationBasedTagResolver().resolve(message);
        assertTrue(tags.contains(new Tag(key, value)));
        assertEquals(1, tags.size());
    }

    @Test
    void callerIsResolvedFromTheCurrentSecurityContext() {
        var context = SecurityContextHolder.getContext();
        context.setAuthentication(null);
        assertFalse(Caller.current().authenticated());
        try {
            var authority = new SimpleGrantedAuthority("ROLE_Sales");
            context.setAuthentication(UsernamePasswordAuthenticationToken.authenticated("sales", null, List.of(authority)));
            assertTrue(Caller.current().authenticated());
            assertEquals("sales", Caller.current().identity());
            assertEquals(Set.of("Sales"), Caller.current().roles());
        } finally { SecurityContextHolder.clearContext(); }
    }

    @Test
    void uniquenessIsOwnerScoped() {
        var store = new InMemoryUniqueValueStore();
        assertTrue(store.claim("email", "value", "first"));
        assertTrue(store.claim("email", "value", "first"));
        assertFalse(store.claim("email", "value", "second"));
        store.release("email", "value", "second");
        assertFalse(store.claim("email", "value", "second"));
        store.release("email", "value", "first");
        assertTrue(store.claim("email", "value", "second"));
    }

    @Test
    void validationErrorsSurviveExceptionSerialization() throws Exception {
        var original = new CommandValidationException(List.of(new ValidationError("amount", "Must be positive")));
        var bytes = new ByteArrayOutputStream();
        try (var output = new ObjectOutputStream(bytes)) { output.writeObject(original); }
        try (var input = new ObjectInputStream(new ByteArrayInputStream(bytes.toByteArray()))) {
            var restored = (CommandValidationException) input.readObject();
            assertEquals(original.errors(), restored.errors());
        }
    }

    @Test
    void validationCannotSilentlySucceed() {
        var validation = new ValidationContext();
        validation.require(false, "amount", "Must be positive");
        var exception = assertThrows(CommandValidationException.class, validation::throwIfInvalid);
        assertEquals("amount", exception.errors().getFirst().property());
        var problem = new ProblemDetailsAdvice().validation(exception);
        assertEquals(400, problem.getStatus());
        assertEquals(validation.errors(), problem.getProperties().get("errors"));
    }
}
