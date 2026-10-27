package io.cratis.stage.axon.runtime;

public final class SecuritySupport {
    private SecuritySupport() {}
    public static Caller currentCaller() { return Caller.current(); }
}
