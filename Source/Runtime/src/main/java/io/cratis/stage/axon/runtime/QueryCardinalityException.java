package io.cratis.stage.axon.runtime;

import java.io.Serial;

public final class QueryCardinalityException extends RuntimeException {
    @Serial
    private static final long serialVersionUID = 1L;
    private final String cardinality;
    private final int count;

    public QueryCardinalityException(String cardinality, int count) {
        super("Expected " + cardinality + " query result");
        this.cardinality = cardinality;
        this.count = count;
    }

    public String cardinality() { return cardinality; }

    public int count() { return count; }
}
