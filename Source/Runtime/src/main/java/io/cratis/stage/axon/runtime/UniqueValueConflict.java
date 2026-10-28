package io.cratis.stage.axon.runtime;

public final class UniqueValueConflict extends RuntimeException {
    private static final long serialVersionUID = 1L;
    public UniqueValueConflict(String message) { super(message); }
}
