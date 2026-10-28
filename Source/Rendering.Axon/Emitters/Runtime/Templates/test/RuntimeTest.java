package __RUNTIME__;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonValue;
import io.cratis.fundamentals.concepts.ConceptAs;
import io.cratis.fundamentals.concepts.Generatable;
import io.cratis.fundamentals.concepts.GeneratableInContext;
import io.cratis.fundamentals.concepts.GenerateValue;
import io.cratis.fundamentals.concepts.GenerationContext;
import java.math.BigDecimal;
import java.time.Duration;
import java.time.Instant;
import java.time.LocalDate;
import java.time.LocalTime;
import java.time.OffsetDateTime;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertThrows;
import tools.jackson.core.JacksonException;
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
    public record Text(String value) implements ConceptAs<String> {}
    public record Whole(Long value) implements ConceptAs<Long> {}
    public record Number(BigDecimal value) implements ConceptAs<BigDecimal> {}
    public record Flag(Boolean value) implements ConceptAs<Boolean> {}
    public record Date(LocalDate value) implements ConceptAs<LocalDate> {}
    public record Time(OffsetDateTime value) implements ConceptAs<OffsetDateTime> {}
    public record TimeOfDay(LocalTime value) implements ConceptAs<LocalTime> {}
    public record Elapsed(Duration value) implements ConceptAs<Duration> {}
    public enum Priority {
        LOW(10), HIGH(30);
        private final Integer value;
        Priority(Integer value) { this.value = value; }
        @JsonValue
        public Integer value() { return value; }
        @Override
        public String toString() { return value.toString(); }
        @JsonCreator
        public static Priority fromValue(Integer value) {
            for (var candidate : values()) if (candidate.value.equals(value)) return candidate;
            throw new IllegalArgumentException("Unknown Priority value: " + value);
        }
    }
    public record Status(Priority value) implements ConceptAs<Priority> {}
    public record Prioritized(Priority priority, Status status) {}

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
    void explicitEnumValuesRoundTrip() {
        var mapper = TestSupport.mapper();
        for (var priority : Priority.values()) {
            var json = priority.value().toString();
            assertEquals(json, mapper.writeValueAsString(priority));
            assertEquals(priority, mapper.readValue(json, Priority.class));
            var status = new Status(priority);
            assertEquals(json, mapper.writeValueAsString(status));
            assertEquals(status, mapper.readValue(json, Status.class));
            var nested = new Prioritized(priority, status);
            var nestedJson = "{\"priority\":" + json + ",\"status\":" + json + "}";
            assertEquals(nestedJson, mapper.writeValueAsString(nested));
            assertEquals(nested, mapper.readValue(nestedJson, Prioritized.class));
        }
    }

    @Test
    void invalidEnumNumbersAreRejected() {
        var mapper = TestSupport.mapper();
        for (var invalid : List.of("0", "1", "20", "31")) {
            assertThrows(IllegalArgumentException.class, () -> Priority.fromValue(Integer.valueOf(invalid)));
            assertThrows(JacksonException.class, () -> mapper.readValue(invalid, Priority.class));
            assertThrows(JacksonException.class, () -> mapper.readValue(invalid, Status.class));
        }
    }

    @Test
    void enumMapKeysRoundTrip() {
        var mapper = TestSupport.mapper();
        var values = Map.of(Priority.LOW, "low", Priority.HIGH, "high");
        var json = mapper.writeValueAsString(values);
        assertEquals("low", mapper.readTree(json).get("10").asString());
        assertEquals("high", mapper.readTree(json).get("30").asString());
        assertEquals(values, mapper.readValue(json, new TypeReference<Map<Priority, String>>() {}));
        var concepts = Map.of(new Status(Priority.HIGH), "high");
        var conceptJson = mapper.writeValueAsString(concepts);
        assertEquals("high", mapper.readTree(conceptJson).get("30").asString());
        assertEquals(concepts, mapper.readValue(conceptJson, new TypeReference<Map<Status, String>>() {}));
    }

    @Test
    void timeSpanValuesFollowDotNet() {
        var mapper = TestSupport.mapper();
        var value = Duration.ofNanos(123456700);
        var json = "\"00:00:00.1234567\"";
        assertEquals(json, mapper.writeValueAsString(value));
        assertEquals(json, mapper.writeValueAsString(new Elapsed(value)));
        assertEquals(new Elapsed(value), mapper.readValue(json, Elapsed.class));
        assertEquals("\"00:00:01\"", mapper.writeValueAsString(Duration.ofSeconds(1)));
    }
}
