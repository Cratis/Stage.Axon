// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

package __RUNTIME__;

import java.io.Serial;
import java.util.List;

public final class CommandValidationException extends RuntimeException {
    @Serial
    private static final long serialVersionUID = 1L;
    private final List<ValidationError> errors;

    public CommandValidationException(String message) { this(List.of(new ValidationError("", message))); }

    public CommandValidationException(List<ValidationError> errors) {
        super("Command validation failed");
        this.errors = List.copyOf(errors);
    }

    public List<ValidationError> errors() { return errors; }
}
