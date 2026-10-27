package io.cratis.stage.axon.runtime;

import io.cratis.fundamentals.concepts.ConceptAs;
import java.util.Objects;

public record EventSourceId(String value) implements ConceptAs<String> {
    public EventSourceId {
        Objects.requireNonNull(value, "value");
    }

    @Override
    public String toString() { return value; }
}
