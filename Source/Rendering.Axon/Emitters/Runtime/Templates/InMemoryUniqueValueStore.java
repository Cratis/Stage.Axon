// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

package __RUNTIME__;

import java.util.Objects;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.ConcurrentMap;

public final class InMemoryUniqueValueStore implements UniqueValueStore {
    private record Key(String constraint, Object value) {}
    private final ConcurrentMap<Key, Object> owners = new ConcurrentHashMap<>();

    @Override
    public boolean claim(String constraint, Object value, Object owner) {
        var existing = owners.putIfAbsent(new Key(Objects.requireNonNull(constraint), Objects.requireNonNull(value)), Objects.requireNonNull(owner));
        return existing == null || existing.equals(owner);
    }

    @Override
    public void release(String constraint, Object value, Object owner) {
        owners.remove(new Key(Objects.requireNonNull(constraint), Objects.requireNonNull(value)), Objects.requireNonNull(owner));
    }
}
