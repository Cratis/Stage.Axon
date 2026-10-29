package __RUNTIME__;

import __APPLICATION__.infrastructure.IsFinanceStaffPolicy;
import __APPLICATION__.accounts.registry.active.AndSnapshot;
import __APPLICATION__.concepts.AccountId;
import io.cratis.fundamentals.concepts.ConceptAs;
import java.math.BigDecimal;
import java.time.OffsetDateTime;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.time.Duration;
import java.util.concurrent.CompletionStage;
import org.axonframework.messaging.core.Metadata;
import org.axonframework.messaging.queryhandling.gateway.QueryGateway;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.context.annotation.Import;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RestController;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.security.oauth2.server.resource.authentication.JwtAuthenticationToken;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.ResultActions;
import tools.jackson.databind.json.JsonMapper;
import static org.awaitility.Awaitility.await;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.jwt;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.asyncDispatch;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.header;
import static org.hamcrest.Matchers.startsWith;

@SpringBootTest(properties = "spring.security.oauth2.resourceserver.jwt.audiences=stage-tests")
@AutoConfigureMockMvc
@Import(GeneratedInlinePolicyTest.HandlerDenialEndpoint.class)
class GeneratedInlinePolicyTest {
    @Autowired private MockMvc http;
    @Autowired private IsFinanceStaffPolicy policy;
    @Autowired private JsonMapper mapper;

    public record Number(BigDecimal value) implements ConceptAs<BigDecimal> { }
    public record Time(OffsetDateTime value) implements ConceptAs<OffsetDateTime> { }
    public record Wire(Number amount, Time at, Map<Number, String> byAmount) { }
    public record Priorities(RuntimeTest.Priority priority, RuntimeTest.Status status, Map<RuntimeTest.Priority, String> byPriority, Map<RuntimeTest.Status, String> byStatus) { }

    @Test
    void exactConceptWireFormatUsesTheApplicationMapper() {
        var json = "{\"amount\":100,\"at\":\"2026-10-24T12:00:00+00:00\",\"byAmount\":{\"100\":\"x\"}}";
        assertEquals(json, mapper.writeValueAsString(mapper.readValue(json, Wire.class)));
    }

    @Test
    void explicitEnumValuesUseTheApplicationMapper() {
        for (var number : List.of(10, 30)) {
            var json = "{\"priority\":" + number + ",\"status\":" + number + ",\"byPriority\":{\"" + number + "\":\"x\"},\"byStatus\":{\"" + number + "\":\"x\"}}";
            assertEquals(json, mapper.writeValueAsString(mapper.readValue(json, Priorities.class)));
        }
    }

    @Test
    void invalidEnumNumbersFailBinding() {
        for (var number : List.of(0, 1, 20, 31)) {
            assertThrows(Exception.class, () -> mapper.readValue("{\"priority\":" + number + "}", Priorities.class));
        }
    }

    @Test
    void nativePolicyBeanReadsOnlyJwtAttributes() throws Exception {
        assertTrue(policy.check(caller("Finance Operations")));
        assertFalse(policy.check(caller("Other")));
    }

    @Test
    void compositionsDenyTheWholeRealCommand() throws Exception {
        var body = "{\"id\":\"" + UUID.randomUUID() + "\",\"alias\":\"proof\"}";
        for (var department : List.of("Finance Operations", "Other", "throw", "error")) {
            http.perform(post("/api/accounts/registry/register-account").contentType("application/json").content(body)
                .with(jwt().jwt(token -> token.claim("department", department)))).andExpect(status().isForbidden());
        }
    }

    @Test
    void unsupportedPoliciesDenyWhileSingleClaimPoliciesReachTheHandler() throws Exception {
        var id = UUID.randomUUID().toString();
        for (var name : List.of("and-snapshot", "or-snapshot", "escape-and-snapshot", "escape-or-snapshot")) {
            completed(http.perform(get("/api/accounts/registry/" + name).param("id", id)
                .with(jwt().jwt(token -> token.claim("department", "Finance Operations"))))).andExpect(status().isForbidden());
        }
        for (var name : List.of("account-snapshot", "checked-snapshot")) {
            completed(http.perform(get("/api/accounts/registry/" + name).param("id", id)
                .with(jwt().jwt(token -> token.claim("department", "Finance Operations"))))).andExpect(status().isNotFound());
        }
        http.perform(get("/api/accounts/registry/account-snapshot").param("id", id)).andExpect(status().isUnauthorized());
    }

    @Test
    void checkedClaimsAllowFinanceAndDenyOtherThrowingAndAnonymousCallers() throws Exception {
        var id = registeredAccount();
        completed(http.perform(get("/api/accounts/registry/checked-snapshot").param("id", id)
            .with(jwt().jwt(token -> token.claim("department", "Finance Operations"))))).andExpect(status().isOk());
        for (var department : List.of("Other", "throw", "error")) {
            http.perform(get("/api/accounts/registry/checked-snapshot").param("id", id)
                .with(jwt().jwt(token -> token.claim("department", department)))).andExpect(status().isForbidden());
        }
        http.perform(get("/api/accounts/registry/checked-snapshot").param("id", id)).andExpect(status().isUnauthorized());
    }

    @Test
    void authenticatedOnlyCommandAllowsRolelessCallerAndDeniesAnonymous() throws Exception {
        var body = "{\"id\":\"" + UUID.randomUUID() + "\",\"alias\":\"authenticated\"}";
        completed(http.perform(post("/api/accounts/registry/authenticated-register").contentType("application/json").content(body)
            .with(jwt().authorities()))).andExpect(status().is2xxSuccessful());
        http.perform(post("/api/accounts/registry/authenticated-register").contentType("application/json").content(body))
            .andExpect(status().isUnauthorized());
    }

    @Test
    void anonymousCommandBodiesAreRejectedBeforeBindingAndConceptValidation() throws Exception {
        for (var body : List.of("", "{", "{}", "{\"id\":\"not-a-uuid\",\"alias\":null}")) {
            http.perform(post("/api/accounts/registry/authenticated-register").contentType("application/json").content(body))
                .andExpect(status().isUnauthorized()).andExpect(header().string("WWW-Authenticate", startsWith("Bearer")));
            completed(http.perform(post("/api/accounts/registry/authenticated-register").contentType("application/json").content(body)
                .with(jwt().authorities()))).andExpect(status().isBadRequest());
        }
    }

    @Test
    void anonymousQueryArgumentsAreRejectedBeforeBindingAndConceptValidation() throws Exception {
        http.perform(get("/api/accounts/registry/account-snapshot").param("id", "not-a-uuid"))
            .andExpect(status().isUnauthorized()).andExpect(header().string("WWW-Authenticate", startsWith("Bearer")));
        completed(http.perform(get("/api/accounts/registry/account-snapshot").param("id", "not-a-uuid")
            .with(jwt().jwt(token -> token.claim("department", "Finance Operations"))))).andExpect(status().isBadRequest());
    }

    @Test
    void notPolicyAllowsUnblockedCallerAndDeniesBlockedCaller() throws Exception {
        var id = registeredAccount();
        completed(http.perform(get("/api/accounts/registry/not-blocked-snapshot").param("id", id).with(jwt())))
            .andExpect(status().isOk());
        http.perform(get("/api/accounts/registry/not-blocked-snapshot").param("id", id)
            .with(jwt().authorities(() -> "ROLE_Blocked"))).andExpect(status().isForbidden());
    }

    @Test
    void distinctRoleAndAllowsBothRolesAndDeniesOneRole() throws Exception {
        var id = registeredAccount();
        completed(http.perform(get("/api/accounts/registry/writer-reviewer-snapshot").param("id", id)
            .with(jwt().authorities(() -> "ROLE_Writer", () -> "ROLE_Reviewer")))).andExpect(status().isOk());
        http.perform(get("/api/accounts/registry/writer-reviewer-snapshot").param("id", id)
            .with(jwt().authorities(() -> "ROLE_Writer"))).andExpect(status().isForbidden());
    }

    @Test
    void singleInlinePolicyAllowsNamedCallerAndDeniesOthers() throws Exception {
        var id = registeredAccount();
        completed(http.perform(get("/api/accounts/registry/named-snapshot").param("id", id)
            .with(jwt().jwt(token -> token.subject("writer"))))).andExpect(status().isOk());
        http.perform(get("/api/accounts/registry/named-snapshot").param("id", id)
            .with(jwt().jwt(token -> token.subject("other")))).andExpect(status().isForbidden());
        http.perform(get("/api/accounts/registry/named-snapshot").param("id", id)).andExpect(status().isUnauthorized());
    }

    @Test
    void jwtClaimPolicyAllowsFinanceAndDeniesOtherDepartmentsAndAnonymous() throws Exception {
        var id = registeredAccount();
        completed(http.perform(get("/api/accounts/registry/account-snapshot").param("id", id)
            .with(jwt().jwt(token -> token.claim("department", "Finance Operations"))))).andExpect(status().isOk());
        http.perform(get("/api/accounts/registry/account-snapshot").param("id", id)
            .with(jwt().jwt(token -> token.claim("department", "Other")))).andExpect(status().isForbidden());
        http.perform(get("/api/accounts/registry/account-snapshot").param("id", id)).andExpect(status().isUnauthorized());
    }

    @Test
    void degradedHandlerDenialMapsToForbiddenProblemDetails() throws Exception {
        completed(http.perform(get("/test/degraded-handler").with(jwt())))
            .andExpect(status().isForbidden()).andExpect(jsonPath("$.type").value("urn:cratis:stage-axon:problem:forbidden"));
    }

    // Bypass endpoint policy only in this test to exercise the degraded handler's native denial.
    @RestController
    static class HandlerDenialEndpoint {
        private final QueryGateway queries;
        HandlerDenialEndpoint(QueryGateway queries) { this.queries = queries; }
        @GetMapping("/test/degraded-handler")
        public CompletionStage<AndSnapshot.Result> denied() {
            return queries.query(new AndSnapshot(new AccountId(UUID.randomUUID())), AndSnapshot.Result.class, Metadata.emptyInstance(), null);
        }
    }

    private String registeredAccount() throws Exception {
        var id = UUID.randomUUID().toString();
        completed(http.perform(post("/api/accounts/registry/authenticated-register").contentType("application/json")
            .content("{\"id\":\"" + id + "\",\"alias\":\"proof\"}").with(jwt().authorities())))
            .andExpect(status().is2xxSuccessful());
        await().atMost(Duration.ofSeconds(10)).untilAsserted(() -> completed(http.perform(get("/api/accounts/registry/account-snapshot").param("id", id)
            .with(jwt().jwt(token -> token.claim("department", "Finance Operations"))))).andExpect(status().isOk()));
        return id;
    }

    private static JwtAuthenticationToken caller(String department) {
        return new JwtAuthenticationToken(Jwt.withTokenValue("test").header("alg", "none").subject("writer").claim("department", department).build());
    }

    private ResultActions completed(ResultActions result) throws Exception {
        var response = result.andReturn();
        return response.getRequest().isAsyncStarted() ? http.perform(asyncDispatch(response)) : result;
    }
}
