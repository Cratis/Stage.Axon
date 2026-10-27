package __RUNTIME__;

import __APPLICATION__.work.items.overview.WorkItems;
import org.axonframework.common.configuration.Configuration;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Qualifier;
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
    private LiveStreams live;
    @Autowired
    @Qualifier(ClientProofSupport.VIEW)
    private ReadModelStore<Object, WorkItems> store;
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
        actions = new ClientProofSupport.Actions(configuration, store, live);
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
}
