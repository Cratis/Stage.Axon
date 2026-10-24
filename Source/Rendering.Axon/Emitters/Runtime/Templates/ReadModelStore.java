// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

package __RUNTIME__;

import java.util.List;
import java.util.Optional;
import java.util.function.UnaryOperator;

public interface ReadModelStore<K, V> {
    Optional<V> get(K key);
    List<V> all();
    void put(K key, V value);
    void remove(K key);
    void clear();
    V update(K key, UnaryOperator<V> update);
}
