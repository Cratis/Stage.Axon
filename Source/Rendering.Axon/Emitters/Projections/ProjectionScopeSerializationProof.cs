// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal static class ProjectionScopeSerializationProof
{
    internal const string Source = """
        package __PACKAGE__;

        import java.time.Instant;
        import java.util.ArrayList;
        import java.util.LinkedHashMap;
        import java.util.List;
        import java.util.Map;
        import java.util.Set;
        import org.axonframework.messaging.core.annotation.AnnotationMessageTypeResolver;
        import org.axonframework.messaging.eventhandling.EventMessage;
        import org.axonframework.messaging.eventhandling.GenericEventMessage;
        import org.junit.jupiter.api.Assertions;
        import org.junit.jupiter.api.Test;
        import __STORE__;
        import __MEMORY__;
        import __SUPPORT__;

        class __NAME__ {
            private static final Instant OCCURRED = Instant.parse("2001-02-03T04:05:06Z");

            private static EventMessage message(Object event) {
                var type = new AnnotationMessageTypeResolver().resolve(event.getClass()).orElseThrow();
                return new GenericEventMessage("original-message", type, event,
                    Map.of("correlationId", "original-correlation", "causationId", "original-causation"), OCCURRED);
            }

            @SuppressWarnings("unchecked")
            private static ReadModelStore<Object, __PROJECTOR__.__STATE__> scope(__PROJECTOR__ projector) throws Exception {
                var field = __PROJECTOR__.class.getDeclaredField("scope");
                field.setAccessible(true);
                return (ReadModelStore<Object, __PROJECTOR__.__STATE__>) field.get(projector);
            }

            private static void assertMessage(__PROJECTOR__.StoredScopeMessage stored, Object event) {
                var restored = stored.restore(event);
                Assertions.assertEquals("original-message", restored.identifier());
                Assertions.assertEquals(OCCURRED, restored.timestamp());
                Assertions.assertEquals("original-correlation", restored.metadata().get("correlationId"));
                Assertions.assertEquals("original-causation", restored.metadata().get("causationId"));
            }

            private record Restored(ReadModelStore<Object, __MODEL__> store, __PROJECTOR__ projector) {}

            private static Restored fresh() {
                var store = new InMemoryReadModelStore<Object, __MODEL__>();
                return new Restored(store, new __PROJECTOR__(store));
            }

            private static Restored roundTrip(Restored current) throws Exception {
                var next = fresh();
                var snapshot = scope(current.projector()).get("scope").orElse(null);
                if (snapshot == null) return next;
                var mapper = TestSupport.mapper();
                var decoded = mapper.readValue(mapper.writeValueAsString(snapshot), __PROJECTOR__.__STATE__.class);
                Assertions.assertEquals(snapshot, decoded);
                Assertions.assertEquals(snapshot.keys(), decoded.keys());
                for (var key : decoded.keys()) current.store().get(key).ifPresent(row -> next.store().put(key, row));
                scope(next.projector()).put("scope", decoded);
                return next;
            }

        __SCENARIOS__

            @Test
            void roundTripsPendingAndJoinWithoutReplayingHistory() throws Exception {
                var mapper = TestSupport.mapper();
                var store = new InMemoryReadModelStore<Object, __MODEL__>();
                var projector = new __PROJECTOR__(store);
                var join = __JOIN__;
                var child = __CHILD__;
                var parent = __PARENT__;
                projector.on(join, message(join)__UPDATES__);
                projector.on(child, message(child)__UPDATES__);
                var original = scope(projector).get("scope").orElseThrow();
                Assertions.assertFalse(original.pending().isEmpty());
                Assertions.assertFalse(original.__CACHE__().isEmpty());
                Assertions.assertTrue(store.all().isEmpty());
                var json = mapper.writeValueAsString(original);
                var decoded = mapper.readValue(json, __PROJECTOR__.__STATE__.class);
                Assertions.assertEquals(original, decoded);
                Assertions.assertNotSame(original.pending(), decoded.pending());
                Assertions.assertNotSame(original.__CACHE__(), decoded.__CACHE__());
                var pendingMessage = (__PROJECTOR__.StoredScopeMessage) decoded.pending().getFirst().getClass().getMethod("message").invoke(decoded.pending().getFirst());
                assertMessage(pendingMessage, child);
                assertMessage(decoded.__CACHE__().values().iterator().next().message(), join);
                var bad = json.replace("__DEFERRED_KIND__", "unknown-scope-kind");
                Assertions.assertNotEquals(json, bad);
                Assertions.assertThrows(Exception.class, () -> mapper.readValue(bad, __PROJECTOR__.__STATE__.class));

                var recoveredStore = new InMemoryReadModelStore<Object, __MODEL__>();
                var recovered = new __PROJECTOR__(recoveredStore);
                scope(recovered).put("scope", decoded);
                recovered.on(parent, message(parent)__UPDATES__);
                projector.on(parent, message(parent)__UPDATES__);
                Assertions.assertEquals(Set.copyOf(store.all()), Set.copyOf(recoveredStore.all()));
                Assertions.assertEquals(scope(projector).get("scope"), scope(recovered).get("scope"));
                Assertions.assertTrue(scope(recovered).get("scope").orElseThrow().pending().isEmpty());
                Assertions.assertFalse(original.pending().isEmpty(), "applying decoded state must not mutate the original snapshot");
            }

            @Test
            void preservesPendingOrderAndLatestJoinMessage() throws Exception {
                var current = fresh();
                var join = __JOIN__;
                var child = __CHILD__;
                var parent = __PARENT__;
                var type = new AnnotationMessageTypeResolver().resolve(join.getClass()).orElseThrow();
                current.projector().on(join, message(join)__UPDATES__);
                var latest = new GenericEventMessage("replacement-message", type, join,
                    Map.of("correlationId", "replacement-correlation"), OCCURRED.plusSeconds(60));
                current.projector().on(join, latest__UPDATES__);
                current.projector().on(child, message(child)__UPDATES__);
                var childType = new AnnotationMessageTypeResolver().resolve(child.getClass()).orElseThrow();
                current.projector().on(child, new GenericEventMessage("second-pending", childType, child,
                    Map.of("causationId", "second-cause"), OCCURRED.plusSeconds(120))__UPDATES__);
                var original = scope(current.projector()).get("scope").orElseThrow();
                Assertions.assertEquals(2, original.pending().size());
                Assertions.assertEquals(1, original.__CACHE__().size());
                var recovered = roundTrip(current);
                var decoded = scope(recovered.projector()).get("scope").orElseThrow();
                var first = (__PROJECTOR__.StoredScopeMessage) decoded.pending().get(0).getClass().getMethod("message").invoke(decoded.pending().get(0));
                var second = (__PROJECTOR__.StoredScopeMessage) decoded.pending().get(1).getClass().getMethod("message").invoke(decoded.pending().get(1));
                Assertions.assertEquals("original-message", first.identifier());
                Assertions.assertEquals("second-pending", second.identifier());
                Assertions.assertEquals("replacement-message", decoded.__CACHE__().values().iterator().next().message().identifier());
                Assertions.assertEquals(OCCURRED.plusSeconds(60), decoded.__CACHE__().values().iterator().next().message().timestamp());
                current.projector().on(parent, message(parent)__UPDATES__);
                recovered.projector().on(parent, message(parent)__UPDATES__);
                Assertions.assertEquals(Set.copyOf(current.store().all()), Set.copyOf(recovered.store().all()));
                Assertions.assertEquals(scope(current.projector()).get("scope"), scope(recovered.projector()).get("scope"));
            }

            @Test
            void snapshotsCopyCollectionsInOrderAndRetainNullMessages() throws Exception {
                var projector = new __PROJECTOR__(new InMemoryReadModelStore<Object, __MODEL__>());
                var join = __JOIN__;
                var child = __CHILD__;
                projector.on(join, null__UPDATES__);
                projector.on(child, null__UPDATES__);
                var original = scope(projector).get("scope").orElseThrow();
                var keys = new ArrayList<Object>(List.of("z", "a"));
                var pending = new ArrayList<>(original.pending());
                var cache = new LinkedHashMap<>(original.__CACHE__());
                var value = cache.values().iterator().next();
                cache.clear();
                cache.put("z", value);
                cache.put("a", value);
                var snapshot = new __PROJECTOR__.__STATE__(keys, pending__CACHES__);
                keys.clear();
                pending.clear();
                cache.clear();
                Assertions.assertEquals(List.of("z", "a"), new ArrayList<>(snapshot.__CACHE__().keySet()));
                Assertions.assertEquals(List.of("z", "a"), snapshot.keys());
                Assertions.assertFalse(snapshot.pending().isEmpty());
                Assertions.assertThrows(UnsupportedOperationException.class, () -> snapshot.keys().clear());
                Assertions.assertThrows(UnsupportedOperationException.class, () -> snapshot.pending().clear());
                Assertions.assertThrows(UnsupportedOperationException.class, () -> snapshot.__CACHE__().clear());
                var mapper = TestSupport.mapper();
                var decoded = mapper.readValue(mapper.writeValueAsString(snapshot), __PROJECTOR__.__STATE__.class);
                Assertions.assertEquals(snapshot, decoded);
                Assertions.assertEquals(List.of("z", "a"), new ArrayList<>(decoded.__CACHE__().keySet()));
                Assertions.assertNull(decoded.__CACHE__().values().iterator().next().message().restore(join));
                var metadata = new LinkedHashMap<String, String>();
                metadata.put("z", "last");
                metadata.put("a", "first");
                var stored = new __PROJECTOR__.StoredScopeMessage(false, "id", OCCURRED, metadata);
                metadata.clear();
                Assertions.assertEquals(List.of("z", "a"), new ArrayList<>(stored.metadata().keySet()));
                Assertions.assertThrows(UnsupportedOperationException.class, () -> stored.metadata().clear());
            }
        }
        """;
}
