package __RUNTIME__;

import java.util.ArrayList;
import java.util.List;

public final class ValidationContext {
    private final List<ValidationError> errors = new ArrayList<>();

    public void reject(String property, String message) { errors.add(new ValidationError(property, message)); }
    public void require(boolean condition, String property, String message) { if (!condition) reject(property, message); }
    public List<ValidationError> errors() { return List.copyOf(errors); }
    public void throwIfInvalid() { if (!errors.isEmpty()) throw new CommandValidationException(errors); }
}
