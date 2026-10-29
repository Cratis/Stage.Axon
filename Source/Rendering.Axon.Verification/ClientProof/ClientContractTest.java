package __RUNTIME__;

import __APPLICATION__.work.items.overview.WorkItemsRepository;
import __APPLICATION__.work.items.overview.WorkItemById;
import __APPLICATION__.work.items.overview.WorkItemByIdEndpoint;
import __APPLICATION__.work.items.overview.AllWorkItems;
import __APPLICATION__.work.items.overview.AllWorkItemsEndpoint;
import __APPLICATION__.work.items.overview.WorkItems;
import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.LinkedBlockingQueue;
import java.util.concurrent.TimeUnit;
import org.junit.jupiter.api.Timeout;
import org.reactivestreams.Subscription;
import org.springframework.http.codec.ServerSentEvent;
import reactor.core.publisher.BaseSubscriber;
import reactor.core.publisher.Sinks;
import org.axonframework.messaging.core.Metadata;
import org.axonframework.messaging.queryhandling.gateway.QueryGateway;
import reactor.core.publisher.Flux;
import reactor.util.concurrent.Queues;
import java.time.Duration;
import java.util.List;
import java.util.concurrent.atomic.AtomicBoolean;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertSame;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;
import org.axonframework.common.configuration.Configuration;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.context.annotation.Import;
import org.springframework.security.core.userdetails.User;
import org.springframework.security.core.userdetails.UserDetailsService;
import org.springframework.security.provisioning.InMemoryUserDetailsManager;
import org.springframework.test.context.bean.override.convention.TestBean;

/** Proves the generated TypeScript client end to end against this running application (see {@link ClientProofSupport}). */
@SpringBootTest(webEnvironment = SpringBootTest.WebEnvironment.RANDOM_PORT, properties = {"stage.axon.live.heartbeat=200ms"})
@Import(ClientProofSupport.Probes.class)
class ClientContractTest {
    @Autowired
    private Configuration configuration;
    @Autowired
    private WorkItemsRepository store;
    @Value("${local.server.port}")
    private int port;

    @TestBean(name = "users", methodName = "users")
    private UserDetailsService users;

    static UserDetailsService users() {
        return new InMemoryUserDetailsManager(
            User.withUsername("editor").password("{noop}secret").roles("Editor").build(),
            User.withUsername("viewer").password("{noop}secret").roles("Viewer").build());
    }

    private ClientProofSupport.Actions actions;

    @BeforeEach
    void reset() throws Exception {
        actions = new ClientProofSupport.Actions(configuration, store);
        actions.reset();
    }

    private void scenario(String name) throws Exception { ClientProofSupport.run(name, port, actions); }

    @Test void typedRoundTrip() throws Exception { scenario("round-trip"); }
    @Test void identityAndDenial() throws Exception { scenario("identity"); }
    @Test void validation() throws Exception { scenario("validation"); }
    @Test void conflict() throws Exception { scenario("conflict"); }
    @Test void cardinality() throws Exception { scenario("cardinality"); }
    @Test void listViewSnapshotMatchesTheStream() throws Exception { scenario("list-view"); }
    @Test void raceBetweenTheInitialReadAndACommit() throws Exception { scenario("race"); }
    @Test void rollbacksPublishNothing() throws Exception { scenario("rollback"); }
    @Test void cancellationStopsTheServerSide() throws Exception { scenario("cancellation"); }
    @Test void identitySwitchCutsTheOldPrincipalOff() throws Exception { scenario("identity-switch"); }
    @Test void aPausedConsumerEndsOnTheFinalState() throws Exception { scenario("slow-consumer"); }

    @Test
    @Timeout(15)
    void pausedSseDemandKeepsOnlyABoundedTailAndReachesTheLatestRows() throws Exception {
        var gateway = mock(QueryGateway.class);
        var source = Sinks.many().unicast().<AllWorkItems.Result>onBackpressureBuffer();
        var cancelled = new AtomicBoolean();
        when(gateway.subscriptionQuery(new AllWorkItems(), AllWorkItems.Result.class, Metadata.emptyInstance(), null, Queues.SMALL_BUFFER_SIZE))
            .thenReturn(source.asFlux().doOnCancel(() -> cancelled.set(true)));
        var endpoint = new AllWorkItemsEndpoint(gateway, Duration.ofMillis(10), Duration.ofSeconds(30));
        var discardHeartbeats = new AtomicBoolean();
        var frames = new LinkedBlockingQueue<ServerSentEvent<Object>>();
        var subscriber = new BaseSubscriber<ServerSentEvent<Object>>() {
            @Override protected void hookOnSubscribe(Subscription subscription) { request(1); }
            @Override protected void hookOnNext(ServerSentEvent<Object> frame) { frames.add(frame); }
        };
        endpoint.stream(null).filter(frame -> !discardHeartbeats.get() || frame.data() != null).subscribe(subscriber);
        try {
            assertTrue(source.tryEmitNext(new AllWorkItems.Result(List.of(new WorkItems(null, null, "0", null, null)))).isSuccess());
            var initial = frames.poll(5, TimeUnit.SECONDS);
            assertNotNull(initial);
            assertEquals("0", ((WorkItems) ((List<?>) initial.data()).getFirst()).title());
            subscriber.request(1);
            var heartbeat = frames.poll(5, TimeUnit.SECONDS);
            assertNotNull(heartbeat);
            assertEquals("heartbeat", heartbeat.comment());
            discardHeartbeats.set(true);
            for (int value = 1; value <= 1024; value++) {
                assertTrue(source.tryEmitNext(new AllWorkItems.Result(List.of(new WorkItems(null, null, Integer.toString(value), null, null)))).isSuccess());
            }
            assertTrue(frames.isEmpty(), "No further data may be delivered without downstream demand");
            // One data slot each: merge, publish, active concatMap, concatMap prefetch, publishOn, latest.
            int dataDemand = 6;
            subscriber.request(dataDemand);
            String title = "0";
            int delivered = 0;
            while (!title.equals("1024") && delivered < dataDemand) {
                var frame = frames.poll(5, TimeUnit.SECONDS);
                assertNotNull(frame);
                title = ((WorkItems) ((List<?>) frame.data()).getFirst()).title();
                delivered++;
            }
            assertEquals("1024", title, "A bounded tail must reach the latest rows without draining a buffered burst");
        } finally {
            subscriber.cancel();
        }
        assertTrue(cancelled.get());
    }

    @Test
    @Timeout(15)
    void liveArgumentsCannotShadowEndpointLocalsOrFields() throws Exception {
        var query = "query=x&data=x&values=x&shared=x&gateway=x&heartbeat=x&timeout=x&shutdown=x&result=x&payload=x&first=x&tick=x";
        var request = HttpRequest.newBuilder(URI.create("http://127.0.0.1:" + port + "/api/work/items/live-endpoint-collisions?" + query))
            .header("Accept", "text/event-stream").header("Authorization", "Basic ZWRpdG9yOnNlY3JldA==").GET().build();
        try (var client = HttpClient.newHttpClient()) {
            var response = client.send(request, HttpResponse.BodyHandlers.ofInputStream());
            assertEquals(200, response.statusCode());
            try (var reader = new BufferedReader(new InputStreamReader(response.body(), StandardCharsets.UTF_8))) {
                assertEquals("data:null", reader.readLine());
                assertEquals(1, ClientProofSupport.ACTIVE.get());
            }
            actions.perform("active:0"); // Closing the HTTP body must release the native Axon subscription via doFinally.
        }
    }

    @Test
    void cancellingResultsCancelsTheNativePublisher() {
        var gateway = mock(QueryGateway.class);
        var query = new WorkItemById(null);
        var metadata = Metadata.emptyInstance();
        var cancelled = new AtomicBoolean();
        var initial = new WorkItemById.Result(List.of());
        when(gateway.subscriptionQuery(query, WorkItemById.Result.class, metadata, null, Queues.SMALL_BUFFER_SIZE))
            .thenReturn(Flux.just(initial).concatWith(Flux.never()).doOnCancel(() -> cancelled.set(true)));
        var endpoint = new WorkItemByIdEndpoint(gateway, Duration.ofSeconds(1), Duration.ofMinutes(1));
        assertEquals(initial, endpoint.results(query, metadata).take(1).blockLast(Duration.ofSeconds(5)));
        assertTrue(cancelled.get());
    }

    @Test
    void nativeCompletionAndFailurePropagateThroughResults() {
        var gateway = mock(QueryGateway.class);
        var query = new WorkItemById(null);
        var metadata = Metadata.emptyInstance();
        var initial = new WorkItemById.Result(List.of());
        var endpoint = new WorkItemByIdEndpoint(gateway, Duration.ofSeconds(1), Duration.ofMinutes(1));
        when(gateway.subscriptionQuery(query, WorkItemById.Result.class, metadata, null, Queues.SMALL_BUFFER_SIZE)).thenReturn(Flux.just(initial));
        assertEquals(List.of(initial), endpoint.results(query, metadata).collectList().block(Duration.ofSeconds(5)));
        var failure = new IllegalStateException("native publisher failed");
        when(gateway.subscriptionQuery(query, WorkItemById.Result.class, metadata, null, Queues.SMALL_BUFFER_SIZE)).thenReturn(Flux.error(failure));
        assertSame(failure, assertThrows(IllegalStateException.class, () -> endpoint.results(query, metadata).blockFirst(Duration.ofSeconds(5))));
    }
}
