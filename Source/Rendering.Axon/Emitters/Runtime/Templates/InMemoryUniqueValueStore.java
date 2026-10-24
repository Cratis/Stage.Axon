// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

package __RUNTIME__;

import java.util.HashMap;
import java.util.List;
import java.util.Map;
import org.axonframework.messaging.core.Context.ResourceKey;
import org.axonframework.messaging.core.unitofwork.ProcessingContext;

public final class InMemoryUniqueValueStore implements UniqueValueStore {
    private record Key(String constraint, String value) {}
    private static final class Changes {
        private final Map<Key, String> writes = new HashMap<>();
    }
    private final ResourceKey<Changes> resource = ResourceKey.withLabel("stage-unique-" + java.util.UUID.randomUUID());
    private final Map<Key, String> owners = new HashMap<>();
    private final Map<Key, Changes> reservations = new HashMap<>();

    @Override
    public synchronized boolean claim(String constraint, Object value, Object owner) {
        var key = new Key(constraint, UniqueValues.key(List.of(value), false));
        if (reservations.containsKey(key)) return false;
        var existing = owners.putIfAbsent(key, UniqueValues.owner(owner));
        return existing == null || existing.equals(UniqueValues.owner(owner));
    }

    @Override
    public synchronized void release(String constraint, Object value, Object owner) {
        var key = new Key(constraint, UniqueValues.key(List.of(value), false));
        if (!reservations.containsKey(key)) owners.remove(key, UniqueValues.owner(owner));
    }

    @Override
    public synchronized void assign(ProcessingContext context, String constraint, List<?> values, Object owner, boolean ignoreCasing, String message) {
        var changes = changes(context);
        var key = new Key(constraint, UniqueValues.key(values, ignoreCasing));
        var identity = UniqueValues.owner(owner);
        var current = changes.writes.containsKey(key) ? changes.writes.get(key) : owners.get(key);
        if (reservedElsewhere(key, changes) || (current != null && !current.equals(identity))) throw new UniqueValueConflict(message);
        releaseOwner(context, constraint, owner);
        if (!key.value().isEmpty()) {
            reservations.put(key, changes);
            changes.writes.put(key, identity);
        }
    }

    @Override
    public synchronized void releaseOwner(ProcessingContext context, String constraint, Object owner) {
        var changes = changes(context);
        var identity = UniqueValues.owner(owner);
        var effective = new HashMap<>(owners);
        effective.putAll(changes.writes);
        var keys = effective.entrySet().stream().filter(entry -> entry.getKey().constraint().equals(constraint) && identity.equals(entry.getValue())).map(Map.Entry::getKey).toList();
        if (keys.stream().anyMatch(key -> reservedElsewhere(key, changes))) throw new UniqueValueConflict("Unique owner is being changed concurrently");
        for (var key : keys) {
            reservations.put(key, changes);
            changes.writes.put(key, null);
        }
    }

    private boolean reservedElsewhere(Key key, Changes changes) {
        return reservations.containsKey(key) && reservations.get(key) != changes;
    }

    private Changes changes(ProcessingContext context) {
        return context.computeResourceIfAbsent(resource, () -> {
            var changes = new Changes();
            context.whenComplete(completed -> complete(changes, completed.isCommitted()));
            return changes;
        });
    }

    private synchronized void complete(Changes changes, boolean committed) {
        if (committed) changes.writes.forEach((key, owner) -> {
            if (owner == null) owners.remove(key);
            else owners.put(key, owner);
        });
        reservations.entrySet().removeIf(entry -> entry.getValue() == changes);
    }
}
