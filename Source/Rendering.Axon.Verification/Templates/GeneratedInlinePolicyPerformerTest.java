package __RUNTIME__;

import __APPLICATION__.accounts.registry.active.ActiveAccounts;
import __APPLICATION__.concepts.AccountId;
import __RUNTIME_MODULE__.ReadModelStore;
import java.time.Duration;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;
import org.springframework.security.core.authority.SimpleGrantedAuthority;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.ResultActions;
import org.springframework.test.web.servlet.request.MockHttpServletRequestBuilder;
import static org.awaitility.Awaitility.await;
import static org.hamcrest.Matchers.containsString;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.asyncDispatch;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.content;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.request;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

@SpringBootTest
@AutoConfigureMockMvc
class GeneratedInlinePolicyPerformerTest {
    private static final String ROOT = "/api/accounts/registry/";
    @Autowired private MockMvc http;
    @Autowired @Qualifier("__APPLICATION__.accounts.registry.active.ActiveAccounts") private ReadModelStore<Object, ActiveAccounts> store;

    @Test
    void inlinePoliciesProtectPerformedQueriesOverHttp() throws Exception {
        for (var name : List.of("performed-accounts", "live-performed-accounts")) {
            queryResult(name, caller("Finance Operations")).andExpect(status().is2xxSuccessful())
                    .andExpect(content().string(containsString("proof")));
            queryResult(name, caller("Other")).andExpect(status().is4xxClientError());
            http.perform(query(name)).andExpect(status().is4xxClientError());
        }
    }

    @Test
    void inlinePoliciesProtectPerformedQueriesOverSse() throws Exception {
        http.perform(query("live-performed-accounts").with(authentication(caller("Other"))).accept("text/event-stream"))
                .andExpect(status().is4xxClientError());
        var stream = http.perform(query("live-performed-accounts").with(authentication(caller("Finance Operations"))).accept("text/event-stream"))
                .andExpect(status().is2xxSuccessful()).andExpect(request().asyncStarted()).andReturn();
        try {
            await().atMost(Duration.ofSeconds(5)).untilAsserted(() -> assertTrue(stream.getResponse().getContentAsString().contains("proof")));
        } finally {
            stream.getRequest().getAsyncContext().complete();
        }
    }

    private static Authentication caller(String department) {
        var caller = UsernamePasswordAuthenticationToken.authenticated("alice", null, List.of(new SimpleGrantedAuthority("ROLE_Reader")));
        caller.setDetails(Map.of("department", department));
        return caller;
    }

    private MockHttpServletRequestBuilder query(String name) {
        var id = new AccountId(UUID.randomUUID());
        store.put(id, new ActiveAccounts(id, "proof"));
        return get(ROOT + name);
    }

    private ResultActions queryResult(String name, Authentication caller) throws Exception {
        var result = http.perform(query(name).with(authentication(caller)).accept("application/json"));
        if (result.andReturn().getRequest().isAsyncStarted()) {
            var response = result.andExpect(request().asyncStarted()).andReturn();
            return http.perform(asyncDispatch(response));
        }
        return result;
    }
}
