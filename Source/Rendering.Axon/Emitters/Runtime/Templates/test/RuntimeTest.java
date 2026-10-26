// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

package __RUNTIME__;

import io.cratis.fundamentals.concepts.ConceptAs;
import io.cratis.fundamentals.concepts.Generatable;
import io.cratis.fundamentals.concepts.GeneratableInContext;
import io.cratis.fundamentals.concepts.GenerateValue;
import io.cratis.fundamentals.concepts.GenerationContext;
import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.io.ObjectInputStream;
import java.io.ObjectOutputStream;
import java.math.BigDecimal;
import java.time.Duration;
import java.time.Instant;
import java.time.LocalDate;
import java.time.LocalTime;
import java.time.OffsetDateTime;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.Executors;
import java.util.stream.IntStream;
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
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;
import tools.jackson.core.type.TypeReference;

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
    @Event(namespace = "runtime", name = "concept-tag", version = "1")
    public record ConceptTagged(@EventTag(key = "id") Id id) {}
    @Event(namespace = "runtime", name = "fallback-tag", version = "1")
    public record FallbackTagged(@EventTag(key = "eventSourceId") EventSourceId eventSourceId) {}
    public record Text(String value) implements ConceptAs<String> {}
    public record Whole(Long value) implements ConceptAs<Long> {}
    public record Number(BigDecimal value) implements ConceptAs<BigDecimal> {}
    public record Flag(Boolean value) implements ConceptAs<Boolean> {}
    public record Date(LocalDate value) implements ConceptAs<LocalDate> {}
    public record Time(OffsetDateTime value) implements ConceptAs<OffsetDateTime> {}
    public record TimeOfDay(LocalTime value) implements ConceptAs<LocalTime> {}
    public record Elapsed(Duration value) implements ConceptAs<Duration> {}
    public enum State { First, Second }
    public record Status(State value) implements ConceptAs<State> {}

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
    void generationUsesTheSelfTypedMarkers() {
        assertInstanceOf(Generatable.class, Id.create());
        assertInstanceOf(GeneratableInContext.class, TimedId.create(new GenerationContext(Instant.now())));
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
    void exponentDecimalValueUsesPlainText() {
        var mapper = TestSupport.mapper();
        var value = new Number(new BigDecimal("1E+2"));
        var json = mapper.writeValueAsString(value);
        assertEquals("100", json);
        assertEquals(new Number(new BigDecimal("100")), mapper.readValue(json, Number.class));
    }

    @Test
    void exponentDecimalMapKeyUsesPlainText() {
        var mapper = TestSupport.mapper();
        var values = Map.of(new Number(new BigDecimal("1E+2")), "x");
        var json = mapper.writeValueAsString(values);
        assertEquals("{\"100\":\"x\"}", json);
        assertEquals(Map.of(new Number(new BigDecimal("100")), "x"), mapper.readValue(json, new TypeReference<Map<Number, String>>() {}));
    }

    @Test
    void decimalKeysRoundTrip() {
        var mapper = TestSupport.mapper();
        var values = Map.of(new Number(new BigDecimal("1.25")), "x");
        var json = mapper.writeValueAsString(values);
        assertEquals("{\"1.25\":\"x\"}", json);
        assertEquals(values, mapper.readValue(json, new TypeReference<Map<Number, String>>() {}));
    }

    @Test
    void utcTimestampsUseExplicitOffsets() {
        var mapper = TestSupport.mapper();
        var value = new Time(OffsetDateTime.parse("2026-10-24T12:00:00Z"));
        var json = mapper.writeValueAsString(value);
        assertEquals("\"2026-10-24T12:00:00+00:00\"", json);
        assertEquals(value, mapper.readValue(json, Time.class));
        var offset = new Time(OffsetDateTime.parse("2026-10-24T12:00:00.1234567+02:00"));
        assertEquals("\"2026-10-24T12:00:00.1234567+02:00\"", mapper.writeValueAsString(offset));
    }

    @Test
    void localTimesUseSevenFractionalDigits() {
        var mapper = TestSupport.mapper();
        var value = LocalTime.of(12, 34, 56, 123456700);
        var json = "\"12:34:56.1234567\"";
        assertEquals(json, mapper.writeValueAsString(value));
        assertEquals(json, mapper.writeValueAsString(new TimeOfDay(value)));
        assertEquals(new TimeOfDay(value), mapper.readValue(json, TimeOfDay.class));
        assertEquals("\"12:34:56.0000000\"", mapper.writeValueAsString(new TimeOfDay(LocalTime.of(12, 34, 56))));
    }

    @Test
    void stringConceptKeysUseEmbeddedJson() {
        var mapper = TestSupport.mapper();
        var values = Map.of(new Text("example"), "x");
        var json = mapper.writeValueAsString(values);
        assertEquals("x", mapper.readTree(json).get("\"example\"").asString());
        assertEquals(values, mapper.readValue(json, new TypeReference<Map<Text, String>>() {}));
        var time = new Time(OffsetDateTime.parse("2026-10-24T12:00:00Z"));
        var times = Map.of(time, "x");
        var timeJson = mapper.writeValueAsString(times);
        assertEquals("x", mapper.readTree(timeJson).get("\"2026-10-24T12:00:00+00:00\"").asString());
        assertEquals(times, mapper.readValue(timeJson, new TypeReference<Map<Time, String>>() {}));
    }

    @Test
    void enumAndTimeSpanValuesFollowDotNet() {
        var mapper = TestSupport.mapper();
        assertEquals("1", mapper.writeValueAsString(State.Second));
        assertEquals("1", mapper.writeValueAsString(new Status(State.Second)));
        assertEquals(new Status(State.Second), mapper.readValue("1", Status.class));
        var value = Duration.ofNanos(123456700);
        var json = "\"00:00:00.1234567\"";
        assertEquals(json, mapper.writeValueAsString(value));
        assertEquals(json, mapper.writeValueAsString(new Elapsed(value)));
        assertEquals(new Elapsed(value), mapper.readValue(json, Elapsed.class));
        assertEquals("\"00:00:01\"", mapper.writeValueAsString(Duration.ofSeconds(1)));
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
