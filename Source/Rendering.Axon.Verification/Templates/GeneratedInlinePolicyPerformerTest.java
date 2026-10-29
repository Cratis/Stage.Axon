package __RUNTIME__;

import __APPLICATION__.accounts.registry.active.ActiveAccountsRepository;
import __APPLICATION__.accounts.registry.active.LivePerformedAccounts;
import __APPLICATION__.accounts.registry.active.PerformedAccounts;
import java.time.Duration;
import java.util.List;
import java.util.concurrent.TimeUnit;
import org.axonframework.messaging.core.Metadata;
import org.axonframework.messaging.queryhandling.gateway.QueryGateway;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.test.context.bean.override.mockito.MockitoSpyBean;
import org.springframework.test.web.servlet.MockMvc;
import reactor.core.publisher.Flux;
import static org.awaitility.Awaitility.await;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertTrue;
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
    void nonHttpDispatchIsNotGuardedAtTheHandlerAsWarnedByStageAxonAuthorization() throws Exception {
        clearInvocations((Object) repository);
        for (var metadata : List.of(Metadata.emptyInstance(), Metadata.with("username", "writer").and("authorities", "ROLE_Writer"))) {
            // STAGE-AXON-AUTHORIZATION: non-HTTP dispatch not guarded at the handler.
            assertNotNull(queries.query(new PerformedAccounts(), PerformedAccounts.Result.class, metadata, null).get(10, TimeUnit.SECONDS));
            assertNotNull(Flux.from(queries.subscriptionQuery(new LivePerformedAccounts(), LivePerformedAccounts.Result.class, metadata, null, 8)).blockFirst(Duration.ofSeconds(10)));
        }
        verify(repository, atLeastOnce()).findAll();
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
