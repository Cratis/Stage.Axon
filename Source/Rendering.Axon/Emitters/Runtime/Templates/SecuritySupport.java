package __RUNTIME__;

public final class SecuritySupport {
    private SecuritySupport() {}
    public static Caller currentCaller() { return Caller.current(); }
}
