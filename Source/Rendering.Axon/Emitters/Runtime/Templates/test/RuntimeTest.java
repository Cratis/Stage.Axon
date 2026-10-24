// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

package __RUNTIME__;

import java.math.BigDecimal;
import java.time.LocalDate;
import java.time.OffsetDateTime;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.Executors;
import java.util.stream.IntStream;
import org.junit.jupiter.api.Test;
import tools.jackson.core.type.TypeReference;
import static org.junit.jupiter.api.Assertions.*;

class RuntimeTest {
    public record Id(UUID value) implements ConceptAs<UUID>, Generatable<Id> {
        public static Id create() { return new Id(GenerateValue.uuid()); }
        @Override
        public String toString() { return value.toString(); }
    }
    public record TimedId(UUID value) implements ConceptAs<UUID>, GeneratableInContext<TimedId> {
        public static TimedId create(GenerationContext context) {
            if (context == null) throw new IllegalArgumentException("Generation context is required");
            return new TimedId(GenerateValue.uuid());
        }
    }
    @org.axonframework.messaging.eventhandling.annotation.Event(namespace = "runtime", name = "concept-tag", version = "1")
    public record ConceptTagged(@org.axonframework.eventsourcing.annotation.EventTag(key = "id") Id id) {}
    @org.axonframework.messaging.eventhandling.annotation.Event(namespace = "runtime", name = "fallback-tag", version = "1")
    public record FallbackTagged(@org.axonframework.eventsourcing.annotation.EventTag(key = "eventSourceId") EventSourceId eventSourceId) {}
    public record Text(String value) implements ConceptAs<String> {}
    public record Whole(Long value) implements ConceptAs<Long> {}
    public record Number(BigDecimal value) implements ConceptAs<BigDecimal> {}
    public record Flag(Boolean value) implements ConceptAs<Boolean> {}
    public record Date(LocalDate value) implements ConceptAs<LocalDate> {}
    public record Time(OffsetDateTime value) implements ConceptAs<OffsetDateTime> {}

    @Test
    void primaryTagsUsePrimitiveIdentityValuesNotRecordDebugStrings() {
        var value = "00000000-0000-0000-0000-000000000001";
        assertTag(new ConceptTagged(new Id(UUID.fromString(value))), "id", value);
        assertTag(new FallbackTagged(new EventSourceId(value)), "eventSourceId", value);
    }

    private static void assertTag(Object event, String key, String value) {
        var type = new org.axonframework.messaging.core.annotation.AnnotationMessageTypeResolver().resolve(event.getClass()).orElseThrow();
        var message = new org.axonframework.messaging.eventhandling.GenericEventMessage(type, event);
        var tags = new org.axonframework.eventsourcing.eventstore.AnnotationBasedTagResolver().resolve(message);
        assertTrue(tags.contains(new org.axonframework.messaging.eventstreaming.Tag(key, value)));
        assertEquals(1, tags.size());
    }

    @Test
    void callerIsResolvedFromTheCurrentSecurityContext() {
        var context = org.springframework.security.core.context.SecurityContextHolder.getContext();
        context.setAuthentication(null);
        assertFalse(Caller.current().authenticated());
        try {
            var authority = new org.springframework.security.core.authority.SimpleGrantedAuthority("ROLE_Sales");
            context.setAuthentication(org.springframework.security.authentication.UsernamePasswordAuthenticationToken.authenticated("sales", null, java.util.List.of(authority)));
            assertTrue(Caller.current().authenticated());
            assertEquals("sales", Caller.current().identity());
            assertEquals(java.util.Set.of("Sales"), Caller.current().roles());
        } finally { org.springframework.security.core.context.SecurityContextHolder.clearContext(); }
    }

    @Test
    void generationUsesTheSelfTypedMarkers() {
        assertInstanceOf(Generatable.class, Id.create());
        assertInstanceOf(GeneratableInContext.class, TimedId.create(GenerationContext.current()));
        assertThrows(IllegalArgumentException.class, () -> TimedId.create(null));
    }

    @Test
    void conceptsUsePrimitiveJsonAndCanonicalConstructors() {
        var mapper = TestSupport.mapper();
        var values = new Object[] { new Id(UUID.randomUUID()), new Text("value"), new Whole(42L), new Number(new BigDecimal("1.25")), new Flag(true), new Date(LocalDate.of(2026, 10, 24)), new Time(OffsetDateTime.parse("2026-10-24T12:00:00Z")) };
        for (var value : values) {
            var json = mapper.writeValueAsString(value);
            assertFalse(json.contains("value" + "\":"));
            assertEquals(value, mapper.readValue(json, value.getClass()));
        }
        assertEquals("\"example\"", mapper.writeValueAsString(new Text("example")));
    }

    @Test
    void conceptMapKeysRoundTrip() {
        var mapper = TestSupport.mapper();
        var id = new Id(UUID.randomUUID());
        var values = Map.of(id, "entry");
        assertEquals(values, mapper.readValue(mapper.writeValueAsString(values), new TypeReference<Map<Id, String>>() {}));
    }

    @Test
    void storeUpdatesAreAtomicAndListsAreSnapshots() throws Exception {
        var store = new InMemoryReadModelStore<String, Integer>();
        try (var executor = Executors.newFixedThreadPool(4)) {
            var updates = IntStream.range(0, 100).mapToObj(ignored -> CompletableFuture.runAsync(() -> store.update("count", current -> current == null ? 1 : current + 1), executor)).toArray(CompletableFuture<?>[]::new);
            CompletableFuture.allOf(updates).get();
        }
        var snapshot = store.all();
        assertEquals(100, store.get("count").orElseThrow());
        store.clear();
        assertEquals(1, snapshot.size());
        assertTrue(store.all().isEmpty());
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
        var original = new CommandValidationException(java.util.List.of(new ValidationError("amount", "Must be positive")));
        var bytes = new java.io.ByteArrayOutputStream();
        try (var output = new java.io.ObjectOutputStream(bytes)) { output.writeObject(original); }
        try (var input = new java.io.ObjectInputStream(new java.io.ByteArrayInputStream(bytes.toByteArray()))) {
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
