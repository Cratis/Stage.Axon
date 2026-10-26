// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

package __RUNTIME__;

import java.util.List;
import org.axonframework.messaging.core.unitofwork.ProcessingContext;

public interface UniqueValueStore {
    boolean claim(String constraint, Object value, Object owner);
    void release(String constraint, Object value, Object owner);
    void assign(ProcessingContext context,
                String constraint, List<?> values, Object owner, boolean ignoreCasing, String message);
    void releaseOwner(ProcessingContext context,
                      String constraint, Object owner);
}
