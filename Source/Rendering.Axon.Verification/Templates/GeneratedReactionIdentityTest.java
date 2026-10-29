package __RUNTIME__;

import __APPLICATION__.accounts.events.AccountRegistered;
import __APPLICATION__.accounts.events.SystemRegistrationRequested;
import __APPLICATION__.accounts.registry.reactionidentity.RegisterSystemAccountHandler;
import __APPLICATION__.concepts.AccountId;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.charset.StandardCharsets;
import java.time.Duration;
import java.util.Arrays;
import java.util.Base64;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.TimeUnit;
import org.axonframework.common.configuration.Configuration;
import org.axonframework.messaging.eventhandling.EventMessage;
import org.axonframework.messaging.eventhandling.gateway.EventGateway;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.Timeout;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.test.web.server.LocalServerPort;
import org.springframework.context.annotation.Import;
import org.springframework.security.access.annotation.Secured;
import org.springframework.test.annotation.DirtiesContext;
import static org.junit.jupiter.api.Assertions.*;
import static org.awaitility.Awaitility.await;

@SpringBootTest(webEnvironment = SpringBootTest.WebEnvironment.RANDOM_PORT)
@Import(GeneratedNameOnlyMetadataTest.IdentityFixture.class)
@DirtiesContext(classMode = DirtiesContext.ClassMode.AFTER_CLASS)
@Timeout(30)
class GeneratedReactionIdentityTest {
    @Autowired private Configuration configuration;
    @Autowired private GeneratedNameOnlyMetadataTest.IdentityFixture identities;
    @LocalServerPort private int port;

    @Test
    void backgroundRunsAsAuthorizesTheSystemCommandAndRecordsSystemAsActor() throws Exception {
        var handler = Arrays.stream(RegisterSystemAccountHandler.class.getDeclaredMethods()).filter(method -> method.getName().equals("handle")).findFirst().orElseThrow();
        assertArrayEquals(new String[] {"ROLE_SYSTEM"}, handler.getAnnotation(Secured.class).value());
        var id = UUID.randomUUID();
        // The actual background event processor delivers this event without an HTTP request.
        configuration.getComponent(EventGateway.class).publish(List.of(new SystemRegistrationRequested(new AccountId(id), id.toString(), "forged")))
                .get(10, TimeUnit.SECONDS);
        var facts = registered(id);
        assertEquals(1, facts.size());
        assertEquals("system", ((AccountRegistered) facts.getFirst().payload()).actor());
        var metadata = identities.handled;
        assertEquals("system", metadata.get("username"));
        assertEquals("ROLE_SYSTEM", metadata.get("authorities"));
        assertEquals("system", facts.getFirst().metadata().get("username"));
    }

    @Test
    void httpWithoutRunsAsRecordsTheAuthenticatedCaller() throws Exception {
        var id = UUID.randomUUID();
        var request = HttpRequest.newBuilder(URI.create("http://localhost:" + port + "/api/triggers/caller-registration-requested"))
                .header("Content-Type", "application/json")
                .header("Authorization", "Basic " + Base64.getEncoder().encodeToString("operator:principal-proof-password".getBytes(StandardCharsets.UTF_8)))
                .POST(HttpRequest.BodyPublishers.ofString("{\"id\":\"" + id + "\",\"alias\":\"" + id + "\",\"actor\":\"forged\"}")).build();
        var response = HttpClient.newHttpClient().send(request, HttpResponse.BodyHandlers.ofString());
        assertEquals(200, response.statusCode(), response.body());
        var facts = GeneratedIngressSupport.facts(configuration, id);
        assertEquals(1, facts.size());
        assertEquals("operator", ((AccountRegistered) facts.getFirst().payload()).actor());
        assertEquals("operator", facts.getFirst().metadata().get("username"));
    }

    private List<EventMessage> registered(UUID id) throws Exception {
        return await().atMost(Duration.ofSeconds(10)).until(
                () -> GeneratedIngressSupport.facts(configuration, id).stream().filter(event -> event.payload() instanceof AccountRegistered).toList(),
                facts -> !facts.isEmpty());
    }
}
