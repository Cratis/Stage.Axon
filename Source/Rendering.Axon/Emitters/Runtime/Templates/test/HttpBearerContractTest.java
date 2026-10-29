package __RUNTIME__;

import io.cratis.stage.axon.runtime.Caller;
import io.cratis.stage.axon.runtime.SseBridge;
import java.time.Instant;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.SubmissionPublisher;
import java.util.concurrent.atomic.AtomicInteger;
import org.axonframework.messaging.commandhandling.annotation.Command;
import org.axonframework.messaging.commandhandling.annotation.CommandHandler;
import org.axonframework.messaging.commandhandling.gateway.CommandGateway;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.context.annotation.Import;
import org.springframework.http.MediaType;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.stereotype.Component;
import org.springframework.test.context.DynamicPropertyRegistry;
import org.springframework.test.context.DynamicPropertySource;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.MvcResult;
import org.springframework.test.web.servlet.request.MockHttpServletRequestBuilder;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RestController;
import org.springframework.web.servlet.mvc.method.annotation.SseEmitter;
import static org.hamcrest.Matchers.startsWith;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.asyncDispatch;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.content;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.header;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.request;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

@SpringBootTest(properties = "stage.axon.cors.allowed-origins=https://app.example")
@AutoConfigureMockMvc
@Import({HttpBearerContractTest.Handlers.class, HttpBearerContractTest.Endpoints.class})
class HttpBearerContractTest {
    private static final AtomicInteger INVOCATIONS = new AtomicInteger();
    @Autowired private MockMvc http;
    @DynamicPropertySource
    static void jwt(DynamicPropertyRegistry registry) { HttpBearerTokens.register(registry); }
    @Command(name = "HttpBearerWhoAmI")
    public record WhoAmI(String identity) {}
    @BeforeEach
    void reset() { INVOCATIONS.set(0); }

    @Test
    void anonymousIsUnauthorized() throws Exception {
        for (var endpoint : protectedRequests()) {
            var result = http.perform(endpoint).andExpect(status().isUnauthorized()).andExpect(request().asyncNotStarted())
                .andExpect(header().string("WWW-Authenticate", startsWith("Bearer"))).andReturn();
            assertNotEventStream(result);
        }
        assertEquals(0, INVOCATIONS.get());
    }

    @Test
    void expiredTokensAreUnauthorized() throws Exception {
        assertDeniedEverywhere(HttpBearerTokens.token(claims -> claims.issuedAt(Instant.now().minusSeconds(1200))
            .expiresAt(Instant.now().minusSeconds(600))), 401);
    }

    @Test
    void wrongIssuerIsUnauthorized() throws Exception {
        assertDeniedEverywhere(HttpBearerTokens.token(claims -> claims.issuer("https://foreign.example")), 401);
    }

    @Test
    void wrongAudienceIsUnauthorized() throws Exception {
        assertDeniedEverywhere(HttpBearerTokens.token(claims -> claims.audience(List.of("other-api"))), 401);
    }

    @Test
    void foreignSignaturesAreUnauthorized() throws Exception { assertDeniedEverywhere(HttpBearerTokens.foreign(), 401); }

    @Test
    void urlTokensAreNeverAccepted() throws Exception {
        var token = HttpBearerTokens.token("alice", "CONTRACT");
        for (var parameter : List.of("access_token", "authorization")) {
            for (var endpoint : protectedRequests()) {
                var result = http.perform(endpoint.queryParam(parameter, token)).andExpect(status().isUnauthorized())
                    .andExpect(request().asyncNotStarted()).andReturn();
                assertNotEventStream(result);
            }
        }
        assertEquals(0, INVOCATIONS.get());
    }

    @Test
    void validTokensReachJsonAndTheEventStream() throws Exception {
        http.perform(get("/_bearer/json").header("Authorization", bearer())).andExpect(status().isOk()).andExpect(content().string("ok"));
        var result = http.perform(get("/_bearer/stream").accept(MediaType.TEXT_EVENT_STREAM).header("Authorization", bearer()))
            .andExpect(request().asyncStarted()).andReturn();
        assertTrue(result.getResponse().getContentType().startsWith("text/event-stream"));
        assertTrue(awaitContent(result, "data:one"));
        assertEquals(2, INVOCATIONS.get());
    }

    @Test
    void commandsRunAsTheTokenSubject() throws Exception {
        var result = http.perform(post("/_bearer/command").contentType(MediaType.APPLICATION_JSON).content("{\"identity\":\"mallory\"}")
            .header("X-User", "mallory").header("Authorization", bearer())).andExpect(request().asyncStarted()).andReturn();
        result.getAsyncResult(10000);
        http.perform(asyncDispatch(result)).andExpect(status().isOk()).andExpect(content().string("alice"));
        assertEquals(1, INVOCATIONS.get());
    }

    @Test
    void rolesComeFromTheRolesClaim() throws Exception {
        assertDeniedEverywhere(HttpBearerTokens.token(claims -> claims.claims(values -> values.remove("roles")).claim("scope", "CONTRACT")), 403);
    }

    @Test
    void anAllowedOriginStreamsWithTheAuthorizationHeader() throws Exception {
        var result = http.perform(get("/_bearer/stream").accept(MediaType.TEXT_EVENT_STREAM).header("Origin", "https://app.example")
            .header("Authorization", bearer())).andExpect(request().asyncStarted())
            .andExpect(header().string("Access-Control-Allow-Origin", "https://app.example"))
            .andExpect(header().doesNotExist("Access-Control-Allow-Credentials")).andReturn();
        assertTrue(result.getResponse().getContentType().startsWith("text/event-stream"));
        assertTrue(awaitContent(result, "data:one"));
    }

    @Test
    void aDisallowedOriginIsDeniedEvenWithAValidToken() throws Exception {
        for (var endpoint : protectedRequests()) {
            var result = http.perform(endpoint.header("Authorization", bearer()).header("Origin", "https://evil.example"))
                .andExpect(status().isForbidden()).andExpect(request().asyncNotStarted())
                .andExpect(header().doesNotExist("Access-Control-Allow-Origin")).andReturn();
            assertNotEventStream(result);
        }
        assertEquals(0, INVOCATIONS.get());
    }

    @Test
    void noSessionCreatedOrCookieIssued() throws Exception {
        for (var endpoint : protectedRequests()) {
            var result = http.perform(endpoint).andExpect(status().isUnauthorized()).andExpect(header().doesNotExist("Set-Cookie")).andReturn();
            assertTrue(result.getRequest().getSession(false) == null, "bearer mode must not create an HTTP session");
        }
        var result = http.perform(get("/_bearer/json").header("Authorization", bearer())).andExpect(status().isOk())
            .andExpect(header().doesNotExist("Set-Cookie")).andReturn();
        assertTrue(result.getRequest().getSession(false) == null, "bearer mode must not create an HTTP session");
    }

    private void assertDeniedEverywhere(String token, int expectedStatus) throws Exception {
        for (var endpoint : protectedRequests()) {
            var result = http.perform(endpoint.header("Authorization", "Bearer " + token)).andExpect(status().is(expectedStatus))
                .andExpect(request().asyncNotStarted()).andReturn();
            assertNotEventStream(result);
        }
        assertEquals(0, INVOCATIONS.get());
    }

    private static List<MockHttpServletRequestBuilder> protectedRequests() {
        return List.of(get("/_bearer/json"), get("/_bearer/stream").accept(MediaType.TEXT_EVENT_STREAM),
            post("/_bearer/command").contentType(MediaType.APPLICATION_JSON).content("{}"));
    }

    private static void assertNotEventStream(MvcResult result) {
        var type = result.getResponse().getContentType();
        assertTrue(type == null || !type.startsWith("text/event-stream"));
    }

    private static String bearer() { return "Bearer " + HttpBearerTokens.token("alice", "CONTRACT"); }

    private static boolean awaitContent(MvcResult result, String text) throws Exception {
        var deadline = System.nanoTime() + 10_000_000_000L;
        while (System.nanoTime() < deadline) {
            if (result.getResponse().getContentAsString().contains(text)) return true;
            Thread.sleep(50);
        }
        return false;
    }

    @RestController
    static class Endpoints {
        private final CommandGateway commands;
        Endpoints(CommandGateway commands) { this.commands = commands; }
        @GetMapping("/_bearer/json")
        @PreAuthorize("hasRole('CONTRACT')")
        String json() { INVOCATIONS.incrementAndGet(); return "ok"; }
        @GetMapping(value = "/_bearer/stream", produces = MediaType.TEXT_EVENT_STREAM_VALUE)
        @PreAuthorize("hasRole('CONTRACT')")
        SseEmitter stream() {
            INVOCATIONS.incrementAndGet();
            var publisher = new SubmissionPublisher<String>();
            var emitter = SseBridge.stream(publisher);
            publisher.submit("one");
            return emitter;
        }
        @PostMapping("/_bearer/command")
        @PreAuthorize("hasRole('CONTRACT')")
        CompletableFuture<String> command(@RequestBody WhoAmI body) { return commands.send(body, String.class); }
    }

    @Component
    static class Handlers {
        @CommandHandler
        String handle(WhoAmI command) {
            INVOCATIONS.incrementAndGet();
            return Caller.current().identity();
        }
    }
}
