// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

package __RUNTIME__;

import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import java.util.Optional;
import java.util.function.UnaryOperator;

public final class InMemoryReadModelStore<K, V> implements ReadModelStore<K, V> {
    private final Map<K, V> values = new LinkedHashMap<>();

    @Override
    public synchronized Optional<V> get(K key) { return Optional.ofNullable(values.get(key)); }

    @Override
    public synchronized List<V> all() { return List.copyOf(values.values()); }

    @Override
    public synchronized void put(K key, V value) { values.put(Objects.requireNonNull(key), Objects.requireNonNull(value)); }

    @Override
    public synchronized void remove(K key) { values.remove(key); }

    @Override
    public synchronized void clear() { values.clear(); }

    @Override
    public synchronized V update(K key, UnaryOperator<V> update) { return values.compute(Objects.requireNonNull(key), (ignored, current) -> update.apply(current)); }
}
