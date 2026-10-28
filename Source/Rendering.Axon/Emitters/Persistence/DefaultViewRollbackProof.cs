// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence;

internal static class DefaultViewRollbackProof
{
    internal const string Source = """
            // Fail after all discovered handlers ran, regardless of their discovery order. Default
            // delivery already runs in PREPARE_COMMIT, so the failure is injected in COMMIT, before APPLY.
            @Namespace("__PACKAGE__")
            static final class RejectingDelivery {
                final ReadModelStore<Object, __MODEL__> store;
                final AtomicBoolean reject = new AtomicBoolean();
                final AtomicInteger stagedFailures = new AtomicInteger();
                RejectingDelivery(@Qualifier("__QUALIFIER__") ReadModelStore<Object, __MODEL__> store) { this.store = store; }
                @org.axonframework.messaging.core.annotation.SequencingPolicy(__PROJECTOR__.__SEQUENCE__.class)
                @EventHandler
                public void handle(__EVENT_TYPE__ event, ProcessingContext context) {
                    if (!reject.getAndSet(false)) return;
                    var before = List.copyOf(store.all());
                    context.runOnCommit(completed -> {
                        assertTrue(ReadModelTransaction.bind(store, completed).get(__KEY__).isPresent(),
                                "FIXTURE: the real projector must have staged its row before rollback");
                        assertEquals(before, store.all(), "Uncommitted projector state must remain invisible");
                        stagedFailures.incrementAndGet();
                        throw new IllegalStateException("rollback-injected after the real projector staged its write");
                    });
                }
            }

            private static void appendDefault(ConfigurableApplicationContext application) throws Exception {
                application.getBean(UnitOfWorkFactory.class).create().executeWithResult(context -> {
                    EventAppender.forContext(context).append(__EVENT__);
                    return CompletableFuture.<Void>completedFuture(null);
                }).get(20, TimeUnit.SECONDS);
            }

            private static void assertRollbackInvisible(ConfigurableApplicationContext application,
                    ObservedStore store, QueryGateway gateway, List<__LIVE_QUERY__.Result> signals) throws Exception {
                var rejector = application.getBean(RejectingDelivery.class);
                var before = List.copyOf(store.all());
                var writes = store.writes.get();
                var emissions = signals.size();
                var failures = rejector.stagedFailures.get();
                rejector.reject.set(true);
                var error = assertThrows(Exception.class, () -> appendDefault(application));
                var messages = new StringBuilder();
                for (Throwable cause = error; cause != null; cause = cause.getCause()) messages.append(cause.getMessage());
                assertTrue(messages.toString().contains("rollback-injected"), messages.toString());
                assertEquals(failures + 1, rejector.stagedFailures.get(), "The failure must follow a real staged write");
                assertEquals(before, store.all(), "Rollback must leave prior committed rows unchanged");
                assertEquals(writes, store.writes.get(), "A rolled-back delivery must never reach the wrapped store");
                var snapshot = gateway.query(new __LIVE_QUERY__(), __LIVE_QUERY__.Result.class).get(20, TimeUnit.SECONDS);
                assertEquals(normalized(before), normalized(snapshot.rows()), "Query snapshot must exclude rolled-back state");
                assertEquals(emissions, signals.size(), "Rollback must not emit a live invalidation");
            }
        """;
}
