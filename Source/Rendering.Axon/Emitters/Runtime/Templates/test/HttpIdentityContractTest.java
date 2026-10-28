package __RUNTIME__;

import io.cratis.stage.axon.runtime.Caller;
import io.cratis.stage.axon.runtime.SseBridge;
import java.nio.charset.StandardCharsets;
import java.util.Base64;
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
import org.springframework.security.core.userdetails.User;
import org.springframework.security.core.userdetails.UserDetailsService;
import org.springframework.security.provisioning.InMemoryUserDetailsManager;
import org.springframework.stereotype.Component;
import org.springframework.test.context.bean.override.convention.TestBean;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.MvcResult;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RestController;
import org.springframework.web.servlet.mvc.method.annotation.SseEmitter;
import static org.hamcrest.Matchers.startsWith;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.httpBasic;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.asyncDispatch;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.options;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.content;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.header;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.request;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

@SpringBootTest
@AutoConfigureMockMvc
@Import({HttpIdentityContractTest.Handlers.class, HttpIdentityContractTest.Endpoints.class})
class HttpIdentityContractTest {
    static final AtomicInteger INVOCATIONS = new AtomicInteger();

    @Autowired
    private MockMvc http;

    @TestBean(name = "users", methodName = "users")
    private UserDetailsService users;

    static UserDetailsService users() {
        return new InMemoryUserDetailsManager(
            User.withUsername("alice").password("{noop}secret").roles("CONTRACT").build(),
            User.withUsername("mallory").password("{noop}secret").roles("OTHER").build());
    }

    @Command(name = "HttpIdentityWhoAmI")
    public record WhoAmI(String identity) {}

    @BeforeEach
    void reset() { INVOCATIONS.set(0); }

    @Test
    void anonymousCallersAreChallengedOnJsonStreamAndCommand() throws Exception {
        http.perform(get("/_identity/json")).andExpect(status().isUnauthorized()).andExpect(header().string("WWW-Authenticate", startsWith("Basic")));
        http.perform(get("/_identity/stream").accept(MediaType.TEXT_EVENT_STREAM)).andExpect(status().isUnauthorized())
            .andExpect(header().string("WWW-Authenticate", startsWith("Basic")));
        http.perform(post("/_identity/command").contentType(MediaType.APPLICATION_JSON).content("{}")).andExpect(status().isUnauthorized())
            .andExpect(header().string("WWW-Authenticate", startsWith("Basic")));
    }

    @Test
    void browserRequestsAreNeverChallenged() throws Exception {
        for (var browser : List.of("Sec-Fetch-Mode", "X-Requested-With")) {
            http.perform(get("/_identity/json").header(browser, "cors")).andExpect(status().isUnauthorized())
                .andExpect(header().doesNotExist("WWW-Authenticate"));
            http.perform(get("/_identity/denied").header(browser, "cors")).andExpect(status().isUnauthorized())
                .andExpect(header().doesNotExist("WWW-Authenticate"));
        }
    }

    @Test
    void callersWithoutTheRoleAreForbidden() throws Exception {
        http.perform(get("/_identity/json").with(httpBasic("mallory", "secret"))).andExpect(status().isForbidden());
        http.perform(get("/_identity/stream").accept(MediaType.TEXT_EVENT_STREAM).with(httpBasic("mallory", "secret")))
            .andExpect(status().isForbidden()).andExpect(request().asyncNotStarted());
        http.perform(post("/_identity/command").contentType(MediaType.APPLICATION_JSON).content("{}").with(httpBasic("mallory", "secret")))
            .andExpect(status().isForbidden());
        assertEquals(0, INVOCATIONS.get());
    }

    @Test
    void deniedStreamsAreNotEventStreams() throws Exception {
        var result = http.perform(get("/_identity/stream").accept(MediaType.TEXT_EVENT_STREAM).with(httpBasic("mallory", "secret"))).andReturn();
        var type = result.getResponse().getContentType();
        assertTrue(type == null || !type.startsWith("text/event-stream"));
    }

    @Test
    void denyAllIsForbiddenForEveryAuthenticatedCallerAndUnauthorizedForAnonymous() throws Exception {
        http.perform(get("/_identity/denied").with(httpBasic("alice", "secret"))).andExpect(status().isForbidden());
        http.perform(get("/_identity/denied").with(httpBasic("mallory", "secret"))).andExpect(status().isForbidden());
        http.perform(get("/_identity/denied")).andExpect(status().isUnauthorized());
    }

    @Test
    void callersWithTheRoleGetJson() throws Exception {
        http.perform(get("/_identity/json").with(httpBasic("alice", "secret"))).andExpect(status().isOk()).andExpect(content().string("ok"));
    }

    @Test
    void callersWithTheRoleGetTheEventStream() throws Exception {
        var result = http.perform(get("/_identity/stream").accept(MediaType.TEXT_EVENT_STREAM).with(httpBasic("alice", "secret")))
            .andExpect(request().asyncStarted()).andReturn();
        assertTrue(result.getResponse().getContentType().startsWith("text/event-stream"));
        assertTrue(awaitContent(result, "data:one"));
    }

    @Test
    void commandsRunAsThePrincipalAndIgnoreClaimedIdentities() throws Exception {
        var result = http.perform(post("/_identity/command").contentType(MediaType.APPLICATION_JSON).content("{\"identity\":\"mallory\"}")
            .header("X-User", "mallory").with(httpBasic("alice", "secret"))).andReturn();
        result.getAsyncResult(10000);
        var body = http.perform(asyncDispatch(result))
            .andExpect(status().isOk()).andReturn().getResponse().getContentAsString();
        assertEquals("alice", body);
    }

    @Test
    void urlTokensAreNeverAccepted() throws Exception {
        var basic = Base64.getEncoder().encodeToString("alice:secret".getBytes(StandardCharsets.UTF_8));
        http.perform(get("/_identity/json").queryParam("access_token", "x")).andExpect(status().isUnauthorized());
        http.perform(get("/_identity/json").queryParam("authorization", "Basic " + basic)).andExpect(status().isUnauthorized());
        http.perform(get("/_identity/stream").accept(MediaType.TEXT_EVENT_STREAM).queryParam("access_token", "x")).andExpect(status().isUnauthorized());
        http.perform(get("/_identity/stream").accept(MediaType.TEXT_EVENT_STREAM).queryParam("authorization", "Basic " + basic))
            .andExpect(status().isUnauthorized());
    }

    @Test
    void crossOriginPreflightsAreDeniedByDefault() throws Exception {
        var result = http.perform(options("/_identity/command").header("Origin", "https://evil.example")
            .header("Access-Control-Request-Method", "POST")).andReturn();
        assertNull(result.getResponse().getHeader("Access-Control-Allow-Origin"));
    }

    @Test
    void simpleRequestContentTypesCannotReachCommands() throws Exception {
        http.perform(post("/_identity/command").contentType(MediaType.TEXT_PLAIN).content("identity=x").with(httpBasic("alice", "secret")))
            .andExpect(status().isUnsupportedMediaType());
        http.perform(post("/_identity/command").contentType(MediaType.APPLICATION_FORM_URLENCODED).content("identity=x").with(httpBasic("alice", "secret")))
            .andExpect(status().isUnsupportedMediaType());
        assertEquals(0, INVOCATIONS.get());
    }

    static boolean awaitContent(MvcResult result, String text) throws Exception {
        var deadline = System.nanoTime() + 10_000_000_000L;
        while (System.nanoTime() < deadline) {
            if (result.getResponse().getContentAsString().contains(text)) return true;
            Thread.sleep(50);
        }
        return false;
    }

    @Component
    static class Handlers {
        @CommandHandler
        String handle(WhoAmI command) {
            INVOCATIONS.incrementAndGet();
            return Caller.current().identity();
        }
    }

    @RestController
    static class Endpoints {
        private final CommandGateway commands;

        Endpoints(CommandGateway commands) { this.commands = commands; }

        @GetMapping("/_identity/json")
        @PreAuthorize("hasRole('CONTRACT')")
        String json() { return "ok"; }

        @GetMapping(value = "/_identity/stream", produces = MediaType.TEXT_EVENT_STREAM_VALUE)
        @PreAuthorize("hasRole('CONTRACT')")
        SseEmitter stream() {
            var publisher = new SubmissionPublisher<String>();
            var emitter = SseBridge.stream(publisher);
            publisher.submit("one");
            return emitter;
        }

        @PostMapping("/_identity/command")
        @PreAuthorize("hasRole('CONTRACT')")
        CompletableFuture<String> command(@RequestBody WhoAmI body) { return commands.send(body, String.class); }

        @GetMapping("/_identity/denied")
        @PreAuthorize("denyAll()")
        String denied() { return "never"; }
    }
}
