package io.cratis.stage.axon.runtime;

import java.io.Serial;

public final class LiveCapacityException extends RuntimeException {
    @Serial
    private static final long serialVersionUID = 1L;

    public LiveCapacityException() { super("Too many live streams are open. Retry shortly."); }
}
