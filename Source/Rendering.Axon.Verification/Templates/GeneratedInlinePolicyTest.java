package __RUNTIME__;

import __APPLICATION__.accounts.registry.active.ActiveAccounts;
import __APPLICATION__.accounts.registry.register.RegisterAccount;
import __APPLICATION__.accounts.registry.register.RegisterAccountHandler;
import __APPLICATION__.concepts.AccountId;
import __RUNTIME_MODULE__.ReadModelStore;
import java.time.Duration;
import java.util.List;
import java.util.concurrent.TimeUnit;
import org.axonframework.messaging.commandhandling.gateway.CommandGateway;
import java.util.Map;
import java.util.UUID;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.core.env.Environment;
import org.springframework.core.env.Profiles;
import org.springframework.security.access.AccessDeniedException;
import org.springframework.security.authentication.AnonymousAuthenticationToken;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;
import org.springframework.security.core.authority.SimpleGrantedAuthority;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.security.oauth2.server.resource.authentication.JwtAuthenticationToken;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.ResultActions;
import org.springframework.test.web.servlet.request.MockHttpServletRequestBuilder;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.awaitility.Awaitility.await;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.asyncDispatch;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.request;

@SpringBootTest
@AutoConfigureMockMvc
class GeneratedInlinePolicyTest {
    private static final String ROOT = "/api/accounts/registry/";
    @Autowired private MockMvc http;
    @Autowired private Environment environment;
    @Autowired private RegisterAccountHandler handler;
    @Autowired private CommandGateway commands;
    @Autowired @Qualifier("__APPLICATION__.accounts.registry.active.ActiveAccounts") private ReadModelStore<Object, ActiveAccounts> store;

    @Test
    void financeWriterAllowsWhileOtherDepartmentsAndAnonymousDeny() throws Exception {
        var accepted = http.perform(command().with(authentication(caller("Finance Operations"))))
                .andExpect(request().asyncStarted()).andReturn();
        http.perform(asyncDispatch(accepted)).andExpect(status().is2xxSuccessful());
        http.perform(command().with(authentication(caller("Other")))).andExpect(status().is4xxClientError());
        http.perform(command()).andExpect(status().is4xxClientError());
        http.perform(query("account-snapshot").with(authentication(caller("Other")))).andExpect(status().is4xxClientError());
        assertFalse(environment.acceptsProfiles(Profiles.of("demo")), "Demo must not activate by default");
    }

    @Test
    void financeWithoutWriterDeniesTheConjunction() throws Exception {
        var caller = UsernamePasswordAuthenticationToken.authenticated("reader", null, List.of(new SimpleGrantedAuthority("ROLE_Reader")));
        caller.setDetails(Map.of("department", "Finance Operations"));
        http.perform(command().with(authentication(caller))).andExpect(status().is4xxClientError());
    }

    @Test
    void scopeEscapeCannotPromoteSiblingFailure() throws Exception {
        queryResult("escape-and-snapshot", caller("Other")).andExpect(status().is4xxClientError());
        for (var department : List.of("throw", "error")) {
            queryResult("escape-and-snapshot", caller(department)).andExpect(status().is4xxClientError());
            queryResult("escape-or-snapshot", caller(department)).andExpect(status().is4xxClientError());
        }
    }

    @Test
    void inlinePoliciesProtectLiveQueries() throws Exception {
        queryResult("live-accounts", caller("Finance Operations")).andExpect(status().is2xxSuccessful());
        queryResult("live-accounts", caller("Other")).andExpect(status().is4xxClientError());
        http.perform(query("live-accounts").with(authentication(caller("Other"))).accept("text/event-stream"))
                .andExpect(status().is4xxClientError());
        var stream = http.perform(query("live-accounts").with(authentication(caller("Finance Operations"))).accept("text/event-stream"))
                .andExpect(status().is2xxSuccessful()).andExpect(request().asyncStarted()).andReturn();
        try {
            await().atMost(Duration.ofSeconds(5)).untilAsserted(() -> assertTrue(stream.getResponse().getContentAsString().contains("proof")));
        } finally {
            stream.getRequest().getAsyncContext().complete();
        }
    }

    @Test
    void inlineFailureAndAnonymousNeverGrant() throws Exception {
        http.perform(query("checked-snapshot").with(authentication(caller("throw")))).andExpect(status().is4xxClientError());
        http.perform(query("checked-snapshot")).andExpect(status().is4xxClientError());
        var anonymous = new AnonymousAuthenticationToken("test", "anonymous", List.of(new SimpleGrantedAuthority("ROLE_ANONYMOUS")));
        http.perform(query("checked-snapshot").with(authentication(anonymous))).andExpect(status().is4xxClientError());
        var unauthenticated = UsernamePasswordAuthenticationToken.unauthenticated("caller", null);
        http.perform(query("checked-snapshot").with(authentication(unauthenticated))).andExpect(status().is4xxClientError());
    }

    @Test
    void roleGrantOrLegitimatePolicyDenyGrants() throws Exception {
        queryResult("or-snapshot", caller("Other")).andExpect(status().is2xxSuccessful());
        queryResult("checked-snapshot", caller("Other")).andExpect(status().is4xxClientError());
    }

    @Test
    void failedOperandDeniesAndAndOr() throws Exception {
        // A granting left operand must never hide the failing right operand (including OR).
        http.perform(query("and-snapshot").with(authentication(caller("throw")))).andExpect(status().is4xxClientError());
        http.perform(query("or-snapshot").with(authentication(caller("throw")))).andExpect(status().is4xxClientError());
        queryResult("or-snapshot", caller("Other")).andExpect(status().is2xxSuccessful());
    }

    @Test
    void directHandlerAndGatewayCannotBypassThePolicy() throws Exception {
        var previous = SecurityContextHolder.getContext();
        var context = SecurityContextHolder.createEmptyContext();
        context.setAuthentication(caller("Other"));
        SecurityContextHolder.setContext(context);
        try {
            var command = new RegisterAccount(new AccountId(UUID.randomUUID()), "denied");
            assertThrows(AccessDeniedException.class, () -> handler.handle(command, null, null));
            var failure = assertThrows(Exception.class, () -> commands.send(command, Void.class).get(10, TimeUnit.SECONDS));
            Throwable root = failure;
            while (root.getCause() != null && root.getCause() != root) root = root.getCause();
            assertInstanceOf(AccessDeniedException.class, root);
        } finally {
            SecurityContextHolder.setContext(previous);
        }
    }

    @Test
    void jwtClaimsTakePrecedenceOverDetails() throws Exception {
        var jwt = Jwt.withTokenValue("proof").header("alg", "none").claim("sub", "alice").claim("department", "Finance Operations").build();
        var caller = new JwtAuthenticationToken(jwt, List.of(new SimpleGrantedAuthority("ROLE_Writer")));
        caller.setDetails(Map.of("department", "Other"));
        var accepted = http.perform(command().with(authentication(caller)))
                .andExpect(request().asyncStarted()).andReturn();
        http.perform(asyncDispatch(accepted)).andExpect(status().is2xxSuccessful());
    }

    @Test
    void absentClaimsDenyInsteadOfGranting() throws Exception {
        var caller = UsernamePasswordAuthenticationToken.authenticated("alice", null, List.of(new SimpleGrantedAuthority("ROLE_Writer")));
        http.perform(command().with(authentication(caller))).andExpect(status().is4xxClientError());
    }

    private static Authentication caller(String department) {
        var caller = UsernamePasswordAuthenticationToken.authenticated("alice", null, List.of(new SimpleGrantedAuthority("ROLE_Writer")));
        caller.setDetails(Map.of("department", department));
        return caller;
    }

    private static MockHttpServletRequestBuilder command() {
        return post(ROOT + "register-account").contentType("application/json")
                .content("{\"id\":\"" + UUID.randomUUID() + "\",\"alias\":\"proof\"}");
    }

    private MockHttpServletRequestBuilder query(String name) {
        var id = new AccountId(UUID.randomUUID());
        store.put(id, new ActiveAccounts(id, "proof"));
        return get(ROOT + name).param("id", id.value().toString());
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
