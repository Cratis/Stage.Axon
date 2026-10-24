// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

package __RUNTIME__;

public final class SecuritySupport {
    private SecuritySupport() {}
    public static Caller currentCaller() { return Caller.current(); }
}
