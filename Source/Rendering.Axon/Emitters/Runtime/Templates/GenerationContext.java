// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

package __RUNTIME__;

import java.time.Instant;
import java.util.Objects;

public record GenerationContext(Instant now, String tenant) {
    public GenerationContext {
        Objects.requireNonNull(now, "now");
        Objects.requireNonNull(tenant, "tenant");
    }

    public GenerationContext(Instant now) { this(now, "00000000-0000-0000-0000-000000000000"); }
    public static GenerationContext current() { return new GenerationContext(Instant.now()); }
}
