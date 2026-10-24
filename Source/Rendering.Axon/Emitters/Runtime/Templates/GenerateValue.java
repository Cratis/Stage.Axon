// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

package __RUNTIME__;

import java.util.UUID;

public final class GenerateValue {
    private GenerateValue() {}
    public static UUID uuid() { return UUID.randomUUID(); }
}
