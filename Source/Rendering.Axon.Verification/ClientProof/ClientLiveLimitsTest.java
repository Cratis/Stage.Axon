package __RUNTIME__;

import __APPLICATION__.work.items.overview.WorkItems;
import __RUNTIME_MODULE__.LiveStreams;
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

/** Proves stream expiry and capacity of the generated TypeScript client against this running application (see {@link ClientProofSupport}). */
@SpringBootTest(webEnvironment = SpringBootTest.WebEnvironment.RANDOM_PORT, properties = {"stage.axon.live.heartbeat=200ms", "stage.axon.live.timeout=2s", "stage.axon.live.max-streams=2"})
@Import(ClientProofSupport.Probes.class)
class ClientLiveLimitsTest {
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

    @Test void anExpiredStreamReconnectsToAFreshSnapshot() throws Exception { scenario("reconnect"); }
    @Test void aStreamOverCapacityIsReportedUnavailable() throws Exception { scenario("capacity"); }
}
