package __RUNTIME__;

import __APPLICATION__.work.items.overview.WorkItemsRepository;
import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.charset.StandardCharsets;
import java.util.UUID;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import org.axonframework.common.configuration.Configuration;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.Timeout;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.context.annotation.Import;
import org.springframework.security.core.userdetails.User;
import org.springframework.security.core.userdetails.UserDetailsService;
import org.springframework.security.provisioning.InMemoryUserDetailsManager;
import org.springframework.test.context.bean.override.convention.TestBean;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

/** Proves stream expiry of the generated TypeScript client against this running application (see {@link ClientProofSupport}). */
@SpringBootTest(webEnvironment = SpringBootTest.WebEnvironment.RANDOM_PORT, properties = {"stage.axon.live.heartbeat=200ms", "stage.axon.live.timeout=2s"})
@Import(ClientProofSupport.Probes.class)
class ClientLiveLimitsTest {
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

    @Test void anExpiredStreamReconnectsToAFreshSnapshot() throws Exception { scenario("reconnect"); }

    @Test
    @Timeout(15)
    void theFirstFrameIsDataThenHeartbeatsAndLifetimeCompletionCancelAxon() throws Exception {
        var gate = new ClientProofSupport.Gate(false, new CountDownLatch(1), new CountDownLatch(1));
        ClientProofSupport.GATE.set(gate);
        try (var client = HttpClient.newHttpClient()) {
            var response = client.sendAsync(stream("Basic ZWRpdG9yOnNlY3JldA=="), HttpResponse.BodyHandlers.ofInputStream());
            try {
                assertTrue(gate.entered().await(5, TimeUnit.SECONDS));
                Thread.sleep(500); // More than two heartbeat periods, with no initial result yet.
                assertFalse(response.isDone(), "No opening or heartbeat frame may commit the response before data");
            } finally {
                gate.release().countDown();
            }
            var opened = response.get(5, TimeUnit.SECONDS);
            assertEquals(200, opened.statusCode());
            assertTrue(opened.headers().firstValue("Content-Type").orElse("").startsWith("text/event-stream"));
            try (var lines = new BufferedReader(new InputStreamReader(opened.body(), StandardCharsets.UTF_8))) {
                assertTrue(lines.readLine().startsWith("data:"), "The first SSE frame must be data");
                boolean heartbeat = false;
                String line;
                while ((line = lines.readLine()) != null) {
                    assertFalse(line.equals(":open"));
                    if (line.equals(":heartbeat")) heartbeat = true;
                }
                assertTrue(heartbeat, "A quiet stream must send heartbeat comments after data");
            }
            actions.perform("active:0");
        }
    }

    @Test
    @Timeout(15)
    void deniedStreamsStayNonSseBeforeTheFirstFrame() throws Exception {
        try (var client = HttpClient.newHttpClient()) {
            for (var authorization : new String[] { "", "Basic dmlld2VyOnNlY3JldA==" }) {
                var response = client.send(stream(authorization), HttpResponse.BodyHandlers.ofString());
                assertEquals(authorization.isEmpty() ? 401 : 403, response.statusCode());
                assertFalse(response.headers().firstValue("Content-Type").orElse("").startsWith("text/event-stream"));
                assertFalse(response.body().contains("data:") || response.body().contains(":heartbeat") || response.body().contains(":open"));
            }
        }
    }

    private HttpRequest stream(String authorization) {
        var request = HttpRequest.newBuilder(URI.create("http://127.0.0.1:" + port + "/api/work/items/work-item-by-id?workItemId=" + UUID.randomUUID()))
            .header("Accept", "text/event-stream");
        if (!authorization.isEmpty()) request.header("Authorization", authorization);
        return request.GET().build();
    }
}
