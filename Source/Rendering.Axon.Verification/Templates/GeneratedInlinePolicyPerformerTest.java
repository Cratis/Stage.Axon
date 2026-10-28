package __RUNTIME__;

import __APPLICATION__.accounts.registry.active.ActiveAccounts;
import __APPLICATION__.accounts.registry.active.LivePerformedAccounts;
import __APPLICATION__.accounts.registry.active.PerformedAccounts;
import __RUNTIME_MODULE__.ReadModelStore;
import java.time.Duration;
import java.util.List;
import java.util.concurrent.TimeUnit;
import org.axonframework.messaging.core.Metadata;
import org.axonframework.messaging.queryhandling.gateway.QueryGateway;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.test.context.bean.override.mockito.MockitoSpyBean;
import org.springframework.test.web.servlet.MockMvc;
import reactor.core.publisher.Flux;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.mockito.Mockito.clearInvocations;
import static org.mockito.Mockito.verifyNoInteractions;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.jwt;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

@SpringBootTest(properties = "spring.security.oauth2.resourceserver.jwt.audiences=stage-tests")
@AutoConfigureMockMvc
class GeneratedInlinePolicyPerformerTest {
    @Autowired private MockMvc http;
    @Autowired private QueryGateway queries;
    @MockitoSpyBean @Qualifier("__APPLICATION__.accounts.registry.active.ActiveAccounts") private ReadModelStore<Object, ActiveAccounts> store;

    @Test
    void inlinePoliciesProtectPerformedQueriesOverHttp() throws Exception {
        clearInvocations((Object) store);
        http.perform(get("/api/accounts/registry/performed-accounts").with(jwt().jwt(token -> token.claim("department", "Other"))))
            .andExpect(status().isForbidden());
        verifyNoInteractions(store);
    }

    @Test
    void inlinePoliciesProtectPerformedQueriesOverSse() {
        clearInvocations((Object) store);
        for (var metadata : List.of(Metadata.emptyInstance(), Metadata.with("username", "writer").and("authorities", "ROLE_Writer"))) {
            assertThrows(Exception.class, () -> queries.query(new PerformedAccounts(), PerformedAccounts.Result.class, metadata, null).get(10, TimeUnit.SECONDS));
            assertThrows(Exception.class, () -> Flux.from(queries.subscriptionQuery(new LivePerformedAccounts(), LivePerformedAccounts.Result.class, metadata, null, 8)).blockFirst(Duration.ofSeconds(10)));
        }
        verifyNoInteractions(store);
    }
}
