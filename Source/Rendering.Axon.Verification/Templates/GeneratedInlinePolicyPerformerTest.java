package __RUNTIME__;

import __APPLICATION__.accounts.registry.active.ActiveAccountsRepository;
import __APPLICATION__.accounts.registry.active.LivePerformedAccounts;
import __APPLICATION__.accounts.registry.active.PerformedAccounts;
import java.time.Duration;
import java.util.List;
import java.util.concurrent.Callable;
import java.util.concurrent.TimeUnit;
import org.axonframework.messaging.core.Metadata;
import org.axonframework.messaging.queryhandling.gateway.QueryGateway;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.test.context.bean.override.mockito.MockitoSpyBean;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.security.access.AccessDeniedException;
import org.springframework.security.authentication.AuthenticationCredentialsNotFoundException;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.security.oauth2.server.resource.authentication.JwtAuthenticationToken;
import org.springframework.test.web.servlet.MockMvc;
import reactor.core.publisher.Flux;
import static org.awaitility.Awaitility.await;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.mockito.Mockito.clearInvocations;
import static org.mockito.Mockito.atLeastOnce;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.verifyNoInteractions;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.jwt;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.asyncDispatch;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.request;

@SpringBootTest(properties = "spring.security.oauth2.resourceserver.jwt.audiences=stage-tests")
@AutoConfigureMockMvc
class GeneratedInlinePolicyPerformerTest {
    @Autowired private MockMvc http;
    @Autowired private QueryGateway queries;
    @MockitoSpyBean private ActiveAccountsRepository repository;

    @Test
    void inlinePoliciesProtectPerformedQueriesOverHttp() throws Exception {
        clearInvocations((Object) repository);
        http.perform(get("/api/accounts/registry/performed-accounts").with(jwt().jwt(token -> token.claim("department", "Other"))))
            .andExpect(status().isForbidden());
        verifyNoInteractions(repository);
    }

    @Test
    void nonHttpDispatchRequiresTheHandlerThreadsInlinePolicyContext() throws Exception {
        clearInvocations((Object) repository);
        SecurityContextHolder.clearContext();
        for (var metadata : List.of(Metadata.emptyInstance(), Metadata.with("username", "writer").and("authorities", "ROLE_Writer"))) {
            denied(() -> queries.query(new PerformedAccounts(), PerformedAccounts.Result.class, metadata, null).get(10, TimeUnit.SECONDS));
            denied(() -> Flux.from(queries.subscriptionQuery(new LivePerformedAccounts(), LivePerformedAccounts.Result.class, metadata, null, 8)).blockFirst(Duration.ofSeconds(10)));
        }
        verifyNoInteractions(repository);
        var token = Jwt.withTokenValue("fixture").header("alg", "none").subject("finance").claim("department", "Finance Operations").build();
        SecurityContextHolder.getContext().setAuthentication(new JwtAuthenticationToken(token, List.of()));
        try {
            assertNotNull(queries.query(new PerformedAccounts(), PerformedAccounts.Result.class, Metadata.emptyInstance(), null).get(10, TimeUnit.SECONDS));
            assertNotNull(Flux.from(queries.subscriptionQuery(new LivePerformedAccounts(), LivePerformedAccounts.Result.class, Metadata.emptyInstance(), null, 8)).blockFirst(Duration.ofSeconds(10)));
        } finally {
            SecurityContextHolder.clearContext();
        }
        verify(repository, atLeastOnce()).findAll();
    }

    private static void denied(Callable<?> dispatch) {
        Throwable failure = assertThrows(Exception.class, dispatch::call);
        while (failure.getCause() != null) failure = failure.getCause();
        assertTrue(failure instanceof AccessDeniedException || failure instanceof AuthenticationCredentialsNotFoundException, failure.toString());
    }

    @Test
    void inlinePoliciesAllowFinanceAndDenyOtherOrAnonymousHttpCallersAtSnapshotAndSseEndpoints() throws Exception {
        for (var name : List.of("performed-accounts", "live-performed-accounts")) {
            var media = name.equals("live-performed-accounts") ? "text/event-stream" : "application/json";
            http.perform(get("/api/accounts/registry/" + name).accept(media)).andExpect(status().isUnauthorized());
            http.perform(get("/api/accounts/registry/" + name).accept(media).with(jwt().jwt(token -> token.claim("department", "Other"))))
                .andExpect(status().isForbidden());
            var result = http.perform(get("/api/accounts/registry/" + name).accept(media).with(jwt().jwt(token -> token.claim("department", "Finance Operations"))))
                .andExpect(request().asyncStarted()).andReturn();
            if (name.equals("performed-accounts")) {
                http.perform(asyncDispatch(result)).andExpect(status().isOk());
            } else {
                await().atMost(Duration.ofSeconds(10)).untilAsserted(() -> assertTrue(result.getResponse().getContentAsString().contains("data:")));
            }
        }
    }
}
