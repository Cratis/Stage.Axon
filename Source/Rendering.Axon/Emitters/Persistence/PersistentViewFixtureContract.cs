// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence;

internal static class PersistentViewFixtureContract
{
    internal const string Source = """
        // Copyright (c) Cratis. All rights reserved.
        // Licensed under the MIT license. See LICENSE file in the project root for full license information.
        package __PACKAGE__;

        import java.util.List;
        import java.util.Map;
        import java.util.concurrent.TimeUnit;
        import org.axonframework.common.configuration.Configuration;
        import org.axonframework.messaging.eventstreaming.Tag;
        import org.axonframework.messaging.eventhandling.processing.streaming.StreamingEventProcessor;
        import org.axonframework.messaging.eventhandling.processing.streaming.pooled.PooledStreamingEventProcessor;
        import org.springframework.context.ConfigurableApplicationContext;
        import static org.junit.jupiter.api.Assertions.*;
        import static __PACKAGE__.__NAME__Support.*;

        final class __NAME__Fixture {
            private __NAME__Fixture() {}
            static Tag tag(Object event) {
        __TAGS__
                throw new IllegalArgumentException("FIXTURE: no modeled primary tag for " + event.getClass().getName());
            }
            static void assertFixture(ConfigurableApplicationContext application, Map<Integer, String> before) throws Exception {
                var processor = application.getBean(Configuration.class).getComponents(StreamingEventProcessor.class).get("__PACKAGE__");
                assertInstanceOf(PooledStreamingEventProcessor.class, processor, "FIXTURE: C2 persistent views require the actual pooled processor");
                var tags = List.of(__FIXTURE_EVENTS__).stream().map(__NAME__Fixture::tag).toList();
                for (var tag : new java.util.HashSet<>(tags))
                    assertSequences(application, tag, tags.stream().filter(tag::equals).count());
                var deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(20);
                while (!ready(application, before) && System.nanoTime() < deadline) Thread.sleep(25);
                assertTrue(ready(application, before), "FIXTURE: C3 token must advance and modeled view row must exist before injection");
            }
            private static boolean ready(ConfigurableApplicationContext application, Map<Integer, String> before) {
                var checkpoint = tokens(application);
                return !store(application).all().isEmpty() && !checkpoint.equals(before)
                        && checkpoint.values().stream().anyMatch(value -> !value.equals("<null>"));
            }
            private static void assertSequences(ConfigurableApplicationContext application, Tag tag, long count) throws Exception {
                try (var connection = application.getBean(javax.sql.DataSource.class).getConnection()) {
                    connection.setReadOnly(true);
                    try (var statement = connection.prepareStatement("select aggregate_sequence_number from AGGREGATE_EVENT_ENTRY " +
                            "where aggregate_identifier = ? and aggregate_type = ? order by aggregate_sequence_number")) {
                        statement.setString(1, tag.value());
                        statement.setString(2, tag.key());
                        try (var result = statement.executeQuery()) {
                            var sequences = new java.util.ArrayList<Long>();
                            while (result.next()) sequences.add(result.getLong(1));
                            assertEquals(java.util.stream.LongStream.range(0, count).boxed().toList(), sequences,
                                    "FIXTURE: C1 aggregate sequences must be exactly 0..n-1 for " + tag);
                        }
                    }
                }
            }
        }
        """;
}
