// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

package __RUNTIME__;

public interface UniqueValueStore {
    boolean claim(String constraint, Object value, Object owner);
    void release(String constraint, Object value, Object owner);
    void assign(org.axonframework.messaging.core.unitofwork.ProcessingContext context,
                String constraint, java.util.List<?> values, Object owner, boolean ignoreCasing, String message);
    void releaseOwner(org.axonframework.messaging.core.unitofwork.ProcessingContext context,
                      String constraint, Object owner);
}
