package __RUNTIME__;

import __APPLICATION__.accounts.events.AccountRegistered;
import __APPLICATION__.accounts.registry.active.AccountSnapshot;
import __APPLICATION__.accounts.registry.active.ActiveAccounts;
import __APPLICATION__.accounts.registry.register.RegisterAccount;
import __APPLICATION__.concepts.AccountId;
import java.util.UUID;
import java.util.List;
import java.util.concurrent.TimeUnit;
import org.axonframework.common.configuration.Configuration;
import org.axonframework.messaging.commandhandling.gateway.CommandGateway;
import org.axonframework.messaging.eventhandling.conversion.EventConverter;
import org.axonframework.messaging.queryhandling.gateway.QueryGateway;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.Timeout;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.security.access.AccessDeniedException;
import org.springframework.security.authentication.AuthenticationCredentialsNotFoundException;
import org.springframework.test.annotation.DirtiesContext;
import org.springframework.test.context.bean.override.mockito.MockitoSpyBean;
import org.springframework.test.web.servlet.MockMvc;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.Mockito.clearInvocations;
import static org.mockito.Mockito.verifyNoInteractions;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.user;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.asyncDispatch;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.request;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;

@SpringBootTest
@AutoConfigureMockMvc
@DirtiesContext(classMode = DirtiesContext.ClassMode.AFTER_CLASS)
@Timeout(60)
class GeneratedSecurityIngressTest {
    private static final String COMMAND = "/api/accounts/registry/register-account";
    private static final String QUERY = "/api/accounts/registry/account-snapshot";
    @Autowired private MockMvc http;
    @Autowired private CommandGateway commands;
    @Autowired private QueryGateway queries;
    @Autowired private Configuration configuration;
    @MockitoSpyBean private UniqueValueStore claims;
    @Autowired @Qualifier("__APPLICATION__.accounts.registry.active.ActiveAccounts") private ReadModelStore<Object, ActiveAccounts> store;

    @Test
    void httpRejectsUnauthenticatedAndWrongRoleCommandsBeforeSideEffects() throws Exception {
        for (var anonymous : new boolean[] { true, false }) {
            var id = UUID.randomUUID();
            var request = post(COMMAND).contentType("application/json").content(body(id));
            if (!anonymous) request.with(user("mallory").roles("Reader"));
            clearInvocations(claims);
            var response = http.perform(request).andExpect(status().is4xxClientError()).andReturn().getResponse();
            System.out.println("Denied HTTP command (anonymous=" + anonymous + ") status: " + response.getStatus());
            assertEquals(0, GeneratedIngressSupport.facts(configuration, id).size());
            verifyNoInteractions(claims);
        }
    }

    @Test
    void gatewayRejectsUnauthenticatedAndWrongRoleCommandsBeforeSideEffects() throws Exception {
        for (var name : new String[] { null, "mallory" }) {
            var id = UUID.randomUUID();
            clearInvocations(claims);
            var failure = assertThrows(Exception.class, () -> GeneratedIngressSupport.as(name, "Reader", () -> commands.send(
                    new RegisterAccount(new AccountId(id), UUID.randomUUID().toString(), "forged-body-principal"), Void.class).get(10, TimeUnit.SECONDS)));
            denied(failure);
            assertEquals(0, GeneratedIngressSupport.facts(configuration, id).size());
            verifyNoInteractions(claims);
        }
    }

    @Test
    void httpAndGatewayQueriesRejectUnauthenticatedAndWrongRoleCallers() throws Exception {
        var id = UUID.randomUUID();
        var anonymous = http.perform(get(QUERY).param("id", id.toString())).andExpect(status().is4xxClientError()).andReturn().getResponse();
        var wrongRole = http.perform(get(QUERY).param("id", id.toString()).with(user("mallory").roles("Reader")))
                .andExpect(status().is4xxClientError()).andReturn().getResponse();
        System.out.println("Denied HTTP query statuses: anonymous=" + anonymous.getStatus() + ", wrong-role=" + wrongRole.getStatus());
        for (var name : new String[] { null, "mallory" }) {
            var failure = assertThrows(Exception.class, () -> GeneratedIngressSupport.as(name, "Reader", () -> queries.query(
                    new AccountSnapshot(new AccountId(id)), AccountSnapshot.Result.class).get(10, TimeUnit.SECONDS)));
            denied(failure);
        }
        assertEquals(0, GeneratedIngressSupport.facts(configuration, id).size());
    }

    @Test
    void allowedCallersCanQueryThroughBothIngressPaths() throws Exception {
        var id = UUID.randomUUID();
        var expected = new ActiveAccounts(new AccountId(id), "seeded", "alice");
        store.put(expected.id(), expected);
        var response = http.perform(get(QUERY).param("id", id.toString()).with(user("alice").roles("Writer")))
                .andExpect(request().asyncStarted()).andReturn();
        response.getAsyncResult(10000);
        http.perform(asyncDispatch(response))
                .andExpect(status().is2xxSuccessful()).andExpect(jsonPath("$.alias").value("seeded"));
        var result = GeneratedIngressSupport.as("alice", "Writer", () -> queries.query(
                new AccountSnapshot(new AccountId(id)), AccountSnapshot.Result.class).get(10, TimeUnit.SECONDS));
        assertEquals(List.of(expected), result.rows());
    }

    @Test
    void acceptedHttpAndGatewayFactsRecordTheRealCallerNotSpoofedInput() throws Exception {
        var httpId = UUID.randomUUID();
        http.perform(post(COMMAND).with(user("alice").roles("Writer"))
                .header("X-Caller", "forged-header-principal").contentType("application/json").content(body(httpId)))
                .andExpect(status().is2xxSuccessful());
        recordedCaller(httpId);
        var gatewayId = UUID.randomUUID();
        GeneratedIngressSupport.register(commands, gatewayId, UUID.randomUUID().toString()).get(10, TimeUnit.SECONDS);
        recordedCaller(gatewayId);
    }

    private void recordedCaller(UUID id) throws Exception {
        var facts = GeneratedIngressSupport.facts(configuration, id);
        assertEquals(1, facts.size(), "The accepted command must produce a real recorded fact");
        var fact = facts.getFirst().payloadAs(AccountRegistered.class, configuration.getComponent(EventConverter.class));
        assertEquals("alice", fact.actor());
        assertEquals(new AccountId(id), fact.accountId());
    }

    private static String body(UUID id) {
        return "{\"id\":\"" + id + "\",\"alias\":\"" + UUID.randomUUID() + "\",\"actor\":\"forged-body-principal\"}";
    }

    private static void denied(Exception failure) {
        var actual = GeneratedIngressSupport.root(failure);
        assertTrue(actual instanceof AccessDeniedException || actual instanceof AuthenticationCredentialsNotFoundException,
                () -> "Expected method-security denial, got " + actual);
    }
}
