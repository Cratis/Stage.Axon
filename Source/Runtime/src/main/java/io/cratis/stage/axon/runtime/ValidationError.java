package io.cratis.stage.axon.runtime;

import java.io.Serial;
import java.io.Serializable;

public record ValidationError(String property, String message) implements Serializable {
    @Serial
    private static final long serialVersionUID = 1L;
}
