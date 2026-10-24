// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

package __RUNTIME__;

import java.util.Objects;

public record EventSourceId(String value) implements ConceptAs<String> {
    public EventSourceId {
        Objects.requireNonNull(value, "value");
    }

    @Override
    public String toString() { return value; }
}
