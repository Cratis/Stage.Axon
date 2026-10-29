package __RUNTIME__;

import __APPLICATION__.work.items.overview.WorkItemsRepository;
import __APPLICATION__.work.items.overview.WorkItemById;
import __APPLICATION__.work.items.overview.WorkItemByIdEndpoint;
import org.axonframework.messaging.core.Metadata;
import org.axonframework.messaging.queryhandling.gateway.QueryGateway;
import reactor.core.publisher.Flux;
import reactor.util.concurrent.Queues;
import java.time.Duration;
import java.util.List;
import java.util.concurrent.atomic.AtomicBoolean;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertSame;
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
