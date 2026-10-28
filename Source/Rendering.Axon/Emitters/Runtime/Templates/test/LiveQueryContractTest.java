package __RUNTIME__;

import io.cratis.stage.axon.runtime.LiveSnapshots;
import io.cratis.stage.axon.runtime.LiveStreams;
import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.UncheckedIOException;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.concurrent.BlockingQueue;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.CopyOnWriteArrayList;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.Flow;
import java.util.concurrent.LinkedBlockingQueue;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.TimeoutException;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicReference;
import java.util.concurrent.locks.LockSupport;
import java.util.function.Predicate;
import java.util.function.UnaryOperator;
import org.axonframework.common.configuration.Configuration;
import org.axonframework.messaging.core.unitofwork.UnitOfWorkFactory;
import org.axonframework.messaging.queryhandling.QueryUpdateEmitter;
import org.axonframework.messaging.queryhandling.annotation.QueryHandler;
import org.axonframework.messaging.queryhandling.gateway.QueryGateway;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.reactivestreams.Publisher;
import org.reactivestreams.Subscriber;
import org.reactivestreams.Subscription;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.test.system.CapturedOutput;
import org.springframework.boot.test.system.OutputCaptureExtension;
import org.springframework.context.annotation.Import;
import org.springframework.http.MediaType;
import org.springframework.stereotype.Component;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;
import org.springframework.web.servlet.mvc.method.annotation.SseEmitter;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

/**
 * Proves the live query delivery contract (latest-state snapshots) on the real Axon bus and a real HTTP server.
 * Negative assertions are proven with a later committed sentinel state, never with sleeps.
 */
@SpringBootTest(
    webEnvironment = SpringBootTest.WebEnvironment.RANDOM_PORT,
    properties = {"stage.axon.live.heartbeat=200ms", "stage.axon.live.timeout=5s", "stage.axon.live.max-streams=2"})
@Import({LiveQueryContractTest.Handlers.class, LiveQueryContractTest.Endpoints.class})
class LiveQueryContractTest {
    private static final long WAIT = 10;
    private static final Map<String, String> STATE = new ConcurrentHashMap<>();
    private static final AtomicReference<Gate> GATE = new AtomicReference<>();
    private static volatile CountDownLatch cancelled = new CountDownLatch(1);

    @Autowired
    private QueryGateway gateway;
    @Autowired
    private Configuration configuration;
    @Autowired
    private LiveStreams live;
    @Value("${local.server.port}")
    private int port;

    private final HttpClient client = HttpClient.newBuilder().version(HttpClient.Version.HTTP_1_1).build();
    private final List<Collector> collectors = new CopyOnWriteArrayList<>();
    private final List<Sse> streams = new CopyOnWriteArrayList<>();

    public record Probe(String key) {}
    public record ProbeResult(List<String> rows, boolean invalidated) {
        static ProbeResult invalidation() { return new ProbeResult(List.of(), true); }
    }

    record Gate(CountDownLatch entered, CountDownLatch release) {}

    @BeforeEach
    void reset() throws Exception {
        STATE.clear();
        GATE.set(null);
        cancelled = new CountDownLatch(1);
        awaitActive(0);
    }

    @AfterEach
    void close() {
        collectors.forEach(Collector::cancel);
        streams.forEach(Sse::close);
    }

    @Test
    void aCommitDuringTheInitialReadIsNotLost() throws Exception {
        STATE.put("race", "v1");
        var gate = new Gate(new CountDownLatch(1), new CountDownLatch(1));
        GATE.set(gate);
        var collector = collector(10);
        CompletableFuture.runAsync(() -> results("race", observer()).subscribe(collector));
        assertTrue(gate.entered().await(WAIT, TimeUnit.SECONDS), "The initial handler never ran");
        commit("race", "v2", query -> true);
        gate.release().countDown();
        var seen = new ArrayList<String>();
        while (!seen.contains("v2")) seen.add(collector.next());
        var after = collector.poll(500);
        assertTrue(seen.stream().allMatch(value -> value.equals("v1") || value.equals("v2")), seen.toString());
        assertTrue(after == null || after.equals("v2"), "A stale snapshot followed the newer one");
    }

    @Test
    void rollbacksAndUnchangedInvalidationsProduceNoUpdate() throws Exception {
        STATE.put("rollback", "base");
        var collector = collector(10);
        results("rollback", observer()).subscribe(collector);
        assertEquals("base", collector.next());
        rollBack("rollback", "ghost", false);
        rollBack("rollback", "ghost", true);
        commit("rollback", "base", query -> true);
        commit("rollback", "sentinel", query -> true);
        assertEquals("sentinel", collector.next());
    }

    @Test
    void aChangeForOneKeyIsNotDeliveredToAnotherKey() throws Exception {
        STATE.put("a", "a0");
        STATE.put("b", "b0");
        var first = collector(10);
        var second = collector(10);
        results("a", observer()).subscribe(first);
        results("b", observer()).subscribe(second);
        assertEquals("a0", first.next());
        assertEquals("b0", second.next());
        commit("a", "a1", query -> true);
        assertEquals("a1", first.next());
        commit("b", "b1", query -> true);
        assertEquals("b1", second.next());
    }

    @Test
    void aSlowConsumerKeepsOnlyTheLatestSnapshot() throws Exception {
        STATE.put("slow", "v0");
        var collector = collector(1);
        results("slow", observer()).subscribe(collector);
        assertEquals("v0", collector.next());
        var started = System.nanoTime();
        for (var version = 1; version <= 2000; version++) commit("slow", "v" + version, query -> true);
        assertTrue(System.nanoTime() - started < TimeUnit.SECONDS.toNanos(30), "Commits were slowed by a consumer without demand");
        assertNull(collector.error());
        collector.request(1);
        assertEquals("v2000", collector.next());
        commit("slow", "sentinel", query -> true);
        collector.request(1);
        assertEquals("sentinel", collector.next());
        assertNull(collector.error());
    }

    @Test
    void aBlockedConsumerDoesNotBlockCommits() throws Exception {
        STATE.put("blocked", "v0");
        var entered = new CountDownLatch(1);
        var release = new CountDownLatch(1);
        var collector = new Collector(10) {
            @Override
            public void onNext(ProbeResult value) {
                entered.countDown();
                try { release.await(WAIT, TimeUnit.SECONDS); } catch (InterruptedException interrupted) { Thread.currentThread().interrupt(); }
                super.onNext(value);
            }
        };
        collectors.add(collector);
        results("blocked", observer()).subscribe(collector);
        try {
            assertTrue(entered.await(WAIT, TimeUnit.SECONDS));
            var started = System.nanoTime();
            commit("blocked", "v1", query -> true);
            assertTrue(System.nanoTime() - started < TimeUnit.SECONDS.toNanos(2), "A commit waited for a blocked consumer");
        } finally {
            release.countDown();
        }
    }

    @Test
    void cancellingTheFlowCancelsTheAxonSubscription() throws Exception {
        STATE.put("cancel", "v0");
        var collector = collector(1);
        results("cancel", observer()).subscribe(collector);
        assertEquals("v0", collector.next());
        collector.cancel();
        assertTrue(cancelled.await(WAIT, TimeUnit.SECONDS), "Cancellation did not reach the Axon publisher");
    }

    @Test
    void closingTheHttpStreamReleasesTheAxonSubscription() throws Exception {
        STATE.put("disconnect", "v0");
        var opened = System.nanoTime();
        var stream = open("disconnect", null);
        assertEquals("[\"v0\"]", stream.nextData());
        assertEquals(1, live.active());
        stream.close();
        assertTrue(cancelled.await(3, TimeUnit.SECONDS), "The Axon subscription outlived the client");
        // The lifetime is 5s and also cancels, so only a release before it elapsed proves the disconnect did it.
        assertTrue(System.nanoTime() - opened < TimeUnit.MILLISECONDS.toNanos(4500), "The release came from the stream lifetime, not the disconnect");
        awaitActive(0);
    }

    @Test
    void aReconnectStartsWithAFullSnapshotAndIgnoresLastEventId() throws Exception {
        STATE.put("reconnect", "v1");
        var first = open("reconnect", null);
        assertEquals("[\"v1\"]", first.nextData());
        first.close();
        awaitActive(0);
        STATE.put("reconnect", "v2");
        var second = open("reconnect", "42");
        assertEquals("[\"v2\"]", second.nextData());
        commit("reconnect", "v3", query -> true);
        assertEquals("[\"v3\"]", second.nextData());
        assertTrue(first.lines().stream().noneMatch(line -> line.startsWith("id:")));
        assertTrue(second.lines().stream().noneMatch(line -> line.startsWith("id:")));
    }

    @Test
    void theStreamEndsAtTheConfiguredLifetime() throws Exception {
        STATE.put("lifetime", "v0");
        var stream = open("lifetime", null);
        assertEquals("[\"v0\"]", stream.nextData());
        var started = System.nanoTime();
        while (stream.nextLine(15) != null) assertTrue(System.nanoTime() - started < TimeUnit.SECONDS.toNanos(20));
        assertTrue(cancelled.await(5, TimeUnit.SECONDS), "The Axon subscription outlived the stream");
        awaitActive(0);
    }

    @Test
    void anIdleStreamSendsHeartbeatComments() throws Exception {
        STATE.put("heartbeat", "v0");
        var stream = open("heartbeat", null);
        assertEquals("[\"v0\"]", stream.nextData());
        String line;
        do line = stream.nextLine(5); while (line != null && !line.equals(":heartbeat"));
        assertNotNull(line, "No heartbeat comment arrived");
    }

    @Test
    void aFullServerAnswersServiceUnavailableUntilAStreamCloses() throws Exception {
        STATE.put("capacity", "v0");
        var first = open("capacity", null);
        var second = open("capacity", null);
        assertEquals("[\"v0\"]", first.nextData());
        assertEquals("[\"v0\"]", second.nextData());
        var refused = client.send(request("capacity", null), HttpResponse.BodyHandlers.ofString());
        assertEquals(503, refused.statusCode());
        assertTrue(refused.headers().firstValue("Retry-After").isPresent());
        assertTrue(refused.headers().firstValue("Content-Type").orElse("").startsWith("application/problem+json"));
        assertTrue(refused.body().contains("urn:cratis:stage-axon:problem:live-capacity"));
        assertFalse(refused.body().contains("event-stream"));
        first.close();
        awaitActive(1);
        var third = open("capacity", null);
        assertEquals("[\"v0\"]", third.nextData());
    }

    @Test
    @ExtendWith(OutputCaptureExtension.class)
    void streamsWhoseSnapshotIsReadyAtOnceAlwaysOpen(CapturedOutput output) throws Exception {
        for (var attempt = 0; attempt < 200; attempt++) {
            // Sweeps the first send across the moment the request thread leaves the servlet filters.
            var delay = (attempt % 40) * 25_000L;
            var response = client.send(readyRequest(delay), HttpResponse.BodyHandlers.ofInputStream());
            var stream = new Sse(response.body());
            streams.add(stream);
            assertEquals(200, response.statusCode(), "Open " + attempt + " failed with the snapshot ready after " + delay + "ns");
            assertTrue(response.headers().firstValue("Content-Type").orElse("").startsWith(MediaType.TEXT_EVENT_STREAM_VALUE));
            assertTrue(response.headers().allValues("X-Content-Type-Options").size() <= 1, "Two threads wrote the headers of open " + attempt);
            assertEquals(":open", stream.nextLine(WAIT));
            assertEquals("[\"ready\"]", stream.nextData());
            stream.awaitCompletion(attempt);
            stream.close();
        }
        awaitActive(0);
        assertFalse(output.getAll().contains("NullPointerException"), "A stream failed on the server");
        assertFalse(output.getAll().contains("Servlet.service()"), "A stream failed with a server error");
    }

    private Flow.Publisher<ProbeResult> results(String key, UnaryOperator<Publisher<ProbeResult>> observe) {
        var query = new Probe(key);
        return LiveSnapshots.of(
            () -> observe.apply(gateway.subscriptionQuery(query, ProbeResult.class)),
            ProbeResult::invalidated,
            () -> gateway.query(query, ProbeResult.class));
    }

    private static UnaryOperator<Publisher<ProbeResult>> observer() { return Endpoints.observe(); }

    private Collector collector(long demand) {
        var collector = new Collector(demand);
        collectors.add(collector);
        return collector;
    }

    private void commit(String key, String value, Predicate<Probe> matching) throws Exception {
        configuration.getComponent(UnitOfWorkFactory.class).create().executeWithResult(processing -> {
            processing.runOnCommit(ignored -> STATE.put(key, value));
            QueryUpdateEmitter.forContext(processing).emit(Probe.class, matching, ProbeResult::invalidation);
            return CompletableFuture.completedFuture(null);
        }).get(WAIT, TimeUnit.SECONDS);
    }

    private void rollBack(String key, String value, boolean afterCommit) {
        var failed = configuration.getComponent(UnitOfWorkFactory.class).create().executeWithResult(processing -> {
            processing.runOnCommit(ignored -> STATE.put(key, value));
            var updates = QueryUpdateEmitter.forContext(processing);
            if (afterCommit) processing.runOnAfterCommit(ignored -> updates.emit(Probe.class, query -> true, ProbeResult::invalidation));
            else updates.emit(Probe.class, query -> true, ProbeResult::invalidation);
            return CompletableFuture.<Object>failedFuture(new IllegalStateException("rollback"));
        });
        assertThrows(ExecutionException.class, () -> failed.get(WAIT, TimeUnit.SECONDS));
    }

    private void awaitActive(int expected) throws InterruptedException {
        var deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(5);
        while (live.active() != expected && System.nanoTime() < deadline) Thread.sleep(25);
        assertEquals(expected, live.active());
    }

    private HttpRequest request(String key, String lastEventId) {
        var builder = HttpRequest.newBuilder(URI.create("http://localhost:" + port + "/_live/probe?key=" + key)).header("Accept", MediaType.TEXT_EVENT_STREAM_VALUE);
        if (lastEventId != null) builder.header("Last-Event-ID", lastEventId);
        return builder.GET().build();
    }

    private HttpRequest readyRequest(long delay) {
        return HttpRequest.newBuilder(URI.create("http://localhost:" + port + "/_live/ready?delay=" + delay)).header("Accept", MediaType.TEXT_EVENT_STREAM_VALUE).GET().build();
    }

    private Sse open(String key, String lastEventId) throws Exception {
        var response = client.send(request(key, lastEventId), HttpResponse.BodyHandlers.ofInputStream());
        assertEquals(200, response.statusCode());
        var stream = new Sse(response.body());
        streams.add(stream);
        return stream;
    }

    private static final class Sse {
        private final InputStream body;
        private final BufferedReader reader;
        private final List<String> lines = new CopyOnWriteArrayList<>();

        private Sse(InputStream body) {
            this.body = body;
            this.reader = new BufferedReader(new InputStreamReader(body, StandardCharsets.UTF_8));
        }

        List<String> lines() { return lines; }

        String nextLine(long seconds) throws Exception {
            try {
                var line = CompletableFuture.supplyAsync(() -> {
                    try { return reader.readLine(); } catch (IOException closed) { return null; }
                }).get(seconds, TimeUnit.SECONDS);
                if (line != null) lines.add(line);
                return line;
            } catch (TimeoutException timeout) {
                return null;
            }
        }

        void awaitCompletion(int attempt) throws Exception {
            var deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(WAIT);
            while (true) {
                var remaining = Math.max(1, deadline - System.nanoTime());
                String line;
                try {
                    line = CompletableFuture.supplyAsync(() -> {
                        try { return reader.readLine(); } catch (IOException failed) { throw new UncheckedIOException(failed); }
                    }).get(remaining, TimeUnit.NANOSECONDS);
                } catch (TimeoutException timeout) {
                    throw new AssertionError("Open " + attempt + " did not complete within " + WAIT + " seconds", timeout);
                } catch (ExecutionException failed) {
                    throw new AssertionError("Open " + attempt + " ended with a read failure instead of completing", failed.getCause());
                }
                if (line == null) return;
                lines.add(line);
                assertTrue(lines.size() < 10, "Open " + attempt + " did not end after its snapshot");
            }
        }

        String nextData() throws Exception {
            while (true) {
                var line = nextLine(WAIT);
                assertNotNull(line, "The stream ended or went silent before data arrived");
                if (line.startsWith("data:")) return line.substring("data:".length()).strip();
            }
        }

        void close() {
            try { body.close(); } catch (IOException ignored) { /* already closed */ }
        }
    }

    private static class Collector implements Flow.Subscriber<ProbeResult> {
        private final BlockingQueue<String> items = new LinkedBlockingQueue<>();
        private final AtomicReference<Throwable> error = new AtomicReference<>();
        private final AtomicReference<Flow.Subscription> subscription = new AtomicReference<>();
        private final long demand;

        Collector(long demand) { this.demand = demand; }

        @Override
        public void onSubscribe(Flow.Subscription value) {
            subscription.set(value);
            value.request(demand);
        }

        @Override
        public void onNext(ProbeResult value) { items.add(value.rows().getFirst()); }

        @Override
        public void onError(Throwable failure) { error.set(failure); }

        @Override
        public void onComplete() {}

        Throwable error() { return error.get(); }

        void request(long count) { subscription.get().request(count); }

        void cancel() {
            var active = subscription.get();
            if (active != null) active.cancel();
        }

        String next() throws InterruptedException {
            var value = items.poll(WAIT, TimeUnit.SECONDS);
            assertNotNull(value, "No snapshot arrived within " + WAIT + "s; error: " + error.get());
            return value;
        }

        String poll(long millis) throws InterruptedException { return items.poll(millis, TimeUnit.MILLISECONDS); }
    }

    @Component
    static class Handlers {
        @QueryHandler
        ProbeResult handle(Probe query) throws InterruptedException {
            var value = STATE.getOrDefault(query.key(), "none");
            var gate = GATE.getAndSet(null);
            if (gate != null) {
                gate.entered().countDown();
                gate.release().await(WAIT, TimeUnit.SECONDS);
            }
            return new ProbeResult(List.of(value), false);
        }
    }

    @RestController
    static class Endpoints {
        private final QueryGateway gateway;
        private final LiveStreams.Group group;

        Endpoints(QueryGateway gateway, LiveStreams live) {
            this.gateway = gateway;
            this.group = live.group();
        }

        static UnaryOperator<Publisher<ProbeResult>> observe() {
            return source -> target -> source.subscribe(new Subscriber<ProbeResult>() {
                @Override public void onSubscribe(Subscription upstream) {
                    target.onSubscribe(new Subscription() {
                        @Override public void request(long amount) { upstream.request(amount); }
                        @Override public void cancel() { upstream.cancel(); cancelled.countDown(); }
                    });
                }
                @Override public void onNext(ProbeResult value) { target.onNext(value); }
                @Override public void onError(Throwable failure) { target.onError(failure); }
                @Override public void onComplete() { target.onComplete(); }
            });
        }

        @GetMapping(value = "/_live/probe", produces = MediaType.TEXT_EVENT_STREAM_VALUE)
        SseEmitter probe(@RequestParam String key) {
            var query = new Probe(key);
            Flow.Publisher<ProbeResult> results = LiveSnapshots.of(
                () -> observe().apply(gateway.subscriptionQuery(query, ProbeResult.class)),
                ProbeResult::invalidated,
                () -> gateway.query(query, ProbeResult.class));
            return open(results);
        }

        /** A live query whose only snapshot is ready after {@code delay} nanoseconds and that then ends the stream. */
        @GetMapping(value = "/_live/ready", produces = MediaType.TEXT_EVENT_STREAM_VALUE)
        SseEmitter ready(@RequestParam long delay) {
            var snapshot = new ProbeResult(List.of("ready"), false);
            Publisher<ProbeResult> source = target -> target.onSubscribe(new Subscription() {
                private final AtomicBoolean started = new AtomicBoolean();
                @Override public void request(long amount) {
                    if (!started.compareAndSet(false, true)) return;
                    Thread.ofVirtual().start(() -> {
                        LockSupport.parkNanos(delay);
                        target.onNext(snapshot);
                        target.onComplete();
                    });
                }
                @Override public void cancel() {}
            });
            return open(LiveSnapshots.of(() -> source, ProbeResult::invalidated, () -> CompletableFuture.completedFuture(snapshot)));
        }

        private SseEmitter open(Flow.Publisher<ProbeResult> results) {
            return group.open(downstream -> results.subscribe(new Flow.Subscriber<ProbeResult>() {
                @Override public void onSubscribe(Flow.Subscription value) { downstream.onSubscribe(value); }
                @Override public void onNext(ProbeResult value) { downstream.onNext(value.rows()); }
                @Override public void onError(Throwable failure) { downstream.onError(failure); }
                @Override public void onComplete() { downstream.onComplete(); }
            }));
        }
    }
}
