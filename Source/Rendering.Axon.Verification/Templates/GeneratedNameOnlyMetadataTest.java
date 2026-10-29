package __RUNTIME__;

import __APPLICATION__.accounts.registry.register.RegisterAccountHandler;
import __APPLICATION__.accounts.registry.register.RegisterAccount;
import __APPLICATION__.accounts.events.AccountRegistered;
import __APPLICATION__.concepts.AccountId;
import org.axonframework.messaging.commandhandling.gateway.CommandGateway;
import org.axonframework.messaging.core.MessageDispatchInterceptor;
import __APPLICATION__.accounts.registry.active.AccountSnapshotHandler;
import __APPLICATION__.accounts.registry.active.ActiveAccounts;
import __APPLICATION__.accounts.registry.active.ActiveAccountsEntity;
import __APPLICATION__.accounts.registry.active.ActiveAccountsRepository;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.util.concurrent.TimeUnit;
import java.net.URI;
import java.net.Socket;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.charset.StandardCharsets;
import java.time.Duration;
import java.time.Instant;
import java.util.Base64;
import java.util.List;
import java.util.Set;
import java.util.UUID;
import org.axonframework.common.configuration.Configuration;
import org.axonframework.messaging.commandhandling.CommandMessage;
import org.axonframework.messaging.core.MessageHandlerInterceptor;
import org.axonframework.messaging.core.Metadata;
import org.axonframework.messaging.core.correlation.CorrelationDataProvider;
import org.axonframework.messaging.core.correlation.SimpleCorrelationDataProvider;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.Timeout;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.boot.test.web.server.LocalServerPort;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Import;
import org.springframework.core.annotation.Order;
import org.springframework.security.config.Customizer;
import org.springframework.security.config.annotation.web.builders.HttpSecurity;
import org.springframework.security.core.Authentication;
import org.springframework.security.core.authority.SimpleGrantedAuthority;
import org.springframework.security.core.userdetails.User;
import org.springframework.security.core.userdetails.UserDetails;
import org.springframework.security.core.userdetails.UserDetailsService;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.security.oauth2.jwt.JwtDecoder;
import org.springframework.security.oauth2.server.resource.authentication.JwtAuthenticationToken;
import org.springframework.security.provisioning.InMemoryUserDetailsManager;
import org.springframework.security.web.SecurityFilterChain;
import org.springframework.test.annotation.DirtiesContext;
import org.springframework.test.context.bean.override.mockito.MockitoSpyBean;
import org.springframework.web.servlet.HandlerInterceptor;
import org.springframework.web.servlet.config.annotation.InterceptorRegistry;
import org.springframework.web.servlet.config.annotation.WebMvcConfigurer;
import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.clearInvocations;
import static org.mockito.Mockito.verifyNoInteractions;

@SpringBootTest(webEnvironment = SpringBootTest.WebEnvironment.RANDOM_PORT)
@Import(GeneratedNameOnlyMetadataTest.IdentityFixture.class)
@DirtiesContext(classMode = DirtiesContext.ClassMode.AFTER_CLASS)
@Timeout(60)
class GeneratedNameOnlyMetadataTest {
    private static final String PASSWORD = "principal-proof-password";
    private static final String TOKEN = "principal.proof.raw.jwt.token";
    private static final String COMMAND = "/api/accounts/registry/register-account";
    private static final String QUERY = "/api/accounts/registry/account-snapshot";
    @LocalServerPort private int port;
    @Autowired private Configuration configuration;
    @Autowired private IdentityFixture fixture;
    @Autowired private ActiveAccountsRepository records;
    @MockitoSpyBean private RegisterAccountHandler commands;
    @MockitoSpyBean private AccountSnapshotHandler queries;

    @TestConfiguration(proxyBeanMethods = false)
    static class IdentityFixture implements WebMvcConfigurer {
        volatile Authentication caller;
        volatile Metadata handled;
        volatile Metadata dispatched;

        @Bean
        @Order(0)
        SecurityFilterChain proofIdentities(HttpSecurity http) throws Exception {
            return http.securityMatcher("/api/**").csrf(csrf -> csrf.disable())
                    .authorizeHttpRequests(requests -> requests.anyRequest().permitAll())
                    .httpBasic(Customizer.withDefaults())
                    .oauth2ResourceServer(server -> server.jwt(jwt -> jwt.decoder(proofDecoder())
                            .jwtAuthenticationConverter(token -> new JwtAuthenticationToken(token,
                                    List.of(new SimpleGrantedAuthority("ROLE_" + token.getClaimAsString("role")))))))
                    .build();
        }

        @Bean
        UserDetailsService proofUsers() {
            return new InMemoryUserDetailsManager(User.withUsername("alice").password("{noop}" + PASSWORD).roles("Writer").build(),
                    User.withUsername("reader").password("{noop}" + PASSWORD).roles("Reader").build(),
                    User.withUsername("operator").password("{noop}" + PASSWORD).roles("SYSTEM", "Writer").build(),
                    User.withUsername("system-only").password("{noop}" + PASSWORD).roles("SYSTEM").build());
        }

        // Synthetic decoder only: the proof exercises principal serialization, not signature verification.
        private JwtDecoder proofDecoder() {
            return token -> Jwt.withTokenValue(token).header("alg", "HS256").subject("jwt-alice")
                    .claim("role", token.endsWith("reader") ? "Reader" : "Writer")
                    .issuedAt(Instant.now()).expiresAt(Instant.now().plusSeconds(300)).build();
        }

        @Override
        public void addInterceptors(InterceptorRegistry registry) {
            registry.addInterceptor(new HandlerInterceptor() {
                @Override
                public boolean preHandle(HttpServletRequest request,
                        HttpServletResponse response, Object handler) {
                    caller = (Authentication) request.getUserPrincipal();
                    return true;
                }
            });
        }

        @Bean
        MessageDispatchInterceptor<CommandMessage> captureDispatchedMetadata() {
            return (message, context, chain) -> {
                dispatched = message.metadata();
                return chain.proceed(message, context);
            };
        }

        @Bean
        MessageHandlerInterceptor<CommandMessage> captureCommandMetadata() {
            return (message, context, chain) -> {
                handled = message.metadata();
                return chain.proceed(message, context);
            };
        }

        // Deliberately copy the audit identity, as in the spike, to prove even correlated events stay safe.
        @Bean
        CorrelationDataProvider proofAuditIdentity() { return new SimpleCorrelationDataProvider("username"); }
    }

    @Test
    void basicAndJwtCommandsAndStoredEventsContainOnlyNamesNotPrincipalsOrCredentials() throws Exception {
        for (var jwt : List.of(false, true)) {
            var id = UUID.randomUUID();
            var response = wire(COMMAND, jwt, false, "{\"id\":\"" + id + "\",\"alias\":\"" + id + "\",\"actor\":\"forged\"}");
            assertEquals(200, response.statusCode(), response.body());
            var name = jwt ? "jwt-alice" : "alice";
            if (jwt) assertInstanceOf(Jwt.class, fixture.caller.getPrincipal());
            else assertInstanceOf(UserDetails.class, fixture.caller.getPrincipal());
            assertEquals(name, fixture.caller.getName());
            assertSafe(fixture.handled, name);
            assertEquals(Set.of("username", "authorities"), fixture.handled.keySet());
            var facts = GeneratedIngressSupport.facts(configuration, id);
            assertEquals(1, facts.size());
            assertSafe(facts.getFirst().metadata(), name);
            assertEquals(name, ((AccountRegistered) facts.getFirst().payload()).actor());
            assertNoLeak(facts.getFirst().payload().toString());
            assertFalse(facts.getFirst().metadata().containsKey("authorities"));
            assertNoLeak(response.body());
        }
    }

    @Test
    void basicAndJwtWrongRolesDenyCommandsQueriesAndSubscriptionOpeningWithoutHandlerExecution() throws Exception {
        for (var jwt : List.of(false, true)) {
            clearInvocations(commands, queries);
            var id = UUID.randomUUID();
            var command = wire(COMMAND, jwt, true, "{\"id\":\"" + id + "\",\"alias\":\"" + id + "\",\"actor\":\"forged\"}");
            assertTrue(command.statusCode() >= 400);
            assertTrue(wire(QUERY + "?id=" + id, jwt, true, null).statusCode() >= 400);
            var stream = rawSse(QUERY + "?id=" + id, jwt);
            // R3 owns the already-committed 200/truncated-stream status, not this metadata proof.
            assertFalse(stream.contains("data:"), stream);
            assertEquals(0, GeneratedIngressSupport.facts(configuration, id).size());
            verifyNoInteractions(commands, queries);
        }
    }

    @Test
    void directGatewayWithoutMetadataRecordsNoCallerAndIsDeniedDespiteTheAmbientIdentity() throws Exception {
        var id = UUID.randomUUID();
        var gateway = configuration.getComponent(CommandGateway.class);
        clearInvocations(commands);
        assertThrows(Exception.class, () -> GeneratedIngressSupport.as("alice", "Writer", () -> gateway.send(
                new RegisterAccount(new AccountId(id), id.toString(), "forged"), Void.class).get(10, TimeUnit.SECONDS)));
        assertFalse(fixture.dispatched.containsKey("username"));
        assertFalse(fixture.dispatched.containsKey("authorities"));
        assertEquals(0, GeneratedIngressSupport.facts(configuration, id).size());
        verifyNoInteractions(commands);
    }

    @Test
    void basicAndJwtCanOpenTheGeneratedQueryAndSubscription() throws Exception {
        for (var jwt : List.of(false, true)) {
            var id = UUID.randomUUID();
            records.save(new ActiveAccountsEntity(id.toString(), new ActiveAccounts(new AccountId(id), "seeded", "alice")));
            var path = QUERY + "?id=" + id;
            assertEquals(200, wire(path, jwt, false, null).statusCode());
            var request = HttpRequest.newBuilder(URI.create("http://localhost:" + port + QUERY + "?id=" + id))
                    .timeout(Duration.ofSeconds(15)).header("Authorization", authorization(jwt, false)).header("Accept", "text/event-stream").GET().build();
            var response = HttpClient.newHttpClient().send(request, HttpResponse.BodyHandlers.ofInputStream());
            assertEquals(200, response.statusCode());
            try (var reader = new BufferedReader(new InputStreamReader(response.body(), StandardCharsets.UTF_8))) {
                String line;
                do { line = reader.readLine(); } while (line != null && !line.startsWith("data:"));
                assertNotNull(line, "Authorized subscription must emit an initial value");
                assertNoLeak(line);
            }
        }
    }

    @Test
    void systemTriggerUsesTheDeclaredRunsAsInsteadOfTheHttpCaller() throws Exception {
        for (var name : List.of("operator", "alice", "system-only")) {
            clearInvocations(commands);
            var id = UUID.randomUUID();
            var request = HttpRequest.newBuilder(URI.create("http://localhost:" + port + "/api/triggers/register-requested"))
                    .timeout(Duration.ofSeconds(15)).header("Content-Type", "application/json")
                    .header("Authorization", "Basic " + Base64.getEncoder().encodeToString((name + ":" + PASSWORD).getBytes(StandardCharsets.UTF_8)))
                    .POST(HttpRequest.BodyPublishers.ofString("{\"id\":\"" + id + "\",\"alias\":\"" + id + "\",\"actor\":\"forged\"}")).build();
            var response = HttpClient.newHttpClient().send(request, HttpResponse.BodyHandlers.ofString());
            assertNoLeak(response.body());
            var facts = GeneratedIngressSupport.facts(configuration, id);
            if (!name.equals("alice")) {
                assertEquals(200, response.statusCode(), response.body());
                var metadata = fixture.handled;
                assertSafe(metadata, "system");
                assertEquals(Set.of("username", "authorities"), metadata.keySet());
                var authorities = Set.of("ROLE_Writer");
                assertEquals(authorities, Set.of(metadata.get("authorities").split(",")));
                assertEquals(1, facts.size());
                var fact = facts.getFirst();
                assertSafe(fact.metadata(), "system");
                assertEquals("system", ((AccountRegistered) fact.payload()).actor());
                assertFalse(fact.metadata().containsKey("authorities"));
            } else {
                assertTrue(response.statusCode() >= 400, response.body());
                assertEquals(0, facts.size());
                verifyNoInteractions(commands);
            }
        }
    }

    private HttpResponse<String> wire(String path, boolean jwt, boolean denied, String body) throws Exception {
        var request = HttpRequest.newBuilder(URI.create("http://localhost:" + port + path)).timeout(Duration.ofSeconds(15))
                .header("Authorization", authorization(jwt, denied));
        if (body == null) request.GET();
        else request.header("Content-Type", "application/json").POST(HttpRequest.BodyPublishers.ofString(body));
        return HttpClient.newHttpClient().send(request.build(), HttpResponse.BodyHandlers.ofString());
    }

    private String rawSse(String path, boolean jwt) throws Exception {
        try (var socket = new Socket("localhost", port)) {
            socket.setSoTimeout(15000);
            socket.getOutputStream().write(("GET " + path + " HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\nAccept: text/event-stream\r\nAuthorization: "
                    + authorization(jwt, true) + "\r\n\r\n").getBytes(StandardCharsets.US_ASCII));
            return new String(socket.getInputStream().readAllBytes(), StandardCharsets.UTF_8);
        }
    }

    private static String authorization(boolean jwt, boolean denied) {
        return jwt ? "Bearer " + TOKEN + (denied ? ".reader" : "") : "Basic " + Base64.getEncoder()
                .encodeToString(((denied ? "reader" : "alice") + ":" + PASSWORD).getBytes(StandardCharsets.UTF_8));
    }

    private static void assertSafe(Metadata metadata, String name) {
        assertEquals(name, metadata.get("username"));
        assertNoLeak(metadata.toString());
    }

    private static void assertNoLeak(String text) {
        for (var forbidden : List.of(PASSWORD, TOKEN, "password", "tokenValue", "credentials", "accountNonExpired"))
            assertFalse(text.contains(forbidden), "Sensitive principal/credential marker: " + forbidden);
    }
}
