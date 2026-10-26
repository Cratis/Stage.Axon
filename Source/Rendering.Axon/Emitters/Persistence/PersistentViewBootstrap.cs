// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence;

internal static class PersistentViewBootstrap
{
    internal const string Source = """
        // Copyright (c) Cratis. All rights reserved.
        // Licensed under the MIT license. See LICENSE file in the project root for full license information.
        package __PACKAGE__;

        import jakarta.persistence.EntityManager;
        import jakarta.persistence.EntityManagerFactory;
        import java.util.stream.Stream;
        import org.axonframework.eventsourcing.eventstore.jpa.AggregateEventEntry;
        import org.axonframework.messaging.eventhandling.processing.streaming.token.store.jpa.TokenEntry;

        /** Validate before constructing any persistent view store or its projector. No replay or reset. */
        public final class PersistentViewBootstrap {
            private PersistentViewBootstrap() {}
            public static void verify(EntityManagerFactory factory, String application, String fingerprint) {
                try (var manager = factory.createEntityManager()) {
                    var transaction = manager.getTransaction();
                    transaction.begin();
                    try {
                        var id = JpaReadModelStore.digest(application);
                        var existing = manager.find(ViewGeneration.class, id);
                        if (existing == null) {
                            if (hasRetainedData(manager))
                                throw refusal(application);
                            manager.persist(new ViewGeneration(id, fingerprint));
                        } else if (!existing.fingerprint().equals(fingerprint)) throw refusal(application);
                        transaction.commit();
                    } catch (RuntimeException failure) {
                        if (transaction.isActive()) transaction.rollback();
                        throw failure;
                    }
                }
            }
            private static boolean hasRetainedData(EntityManager manager) {
                return Stream.of(AggregateEventEntry.class, TokenEntry.class, ViewRow.class)
                        .anyMatch(type -> count(manager, type) != 0);
            }
            private static long count(EntityManager manager, Class<?> type) {
                var name = manager.getMetamodel().entity(type).getName();
                return manager.createQuery("select count(e) from " + name + " e", Long.class).getSingleResult();
            }
            private static IllegalStateException refusal(String application) {
                return new IllegalStateException("STAGE-AXON-0308: " + application + ": Missing or incompatible persistent view generation. Retained events/checkpoints cannot be adopted. Use a separately migrated/rebuilt database or a new empty database; no automatic replay or reset is performed.");
            }
        }
        """;
}
