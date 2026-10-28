package __RUNTIME__;

import __APPLICATION__.infrastructure.IsFinanceStaffPolicy;
import io.cratis.fundamentals.concepts.ConceptAs;
import java.math.BigDecimal;
import java.time.OffsetDateTime;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import jakarta.servlet.ServletException;
import org.axonframework.extension.spring.authorization.UnauthorizedMessageException;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.security.oauth2.server.resource.authentication.JwtAuthenticationToken;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.ResultActions;
import tools.jackson.databind.json.JsonMapper;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.jwt;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.asyncDispatch;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

@SpringBootTest(properties = "spring.security.oauth2.resourceserver.jwt.audiences=stage-tests")
@AutoConfigureMockMvc
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
    void handlerClaimsAndUnprovenSubscriptionUpdatesFailClosed() throws Exception {
        var id = UUID.randomUUID().toString();
        for (var name : List.of("checked-snapshot", "and-snapshot", "or-snapshot", "escape-and-snapshot", "escape-or-snapshot")) {
            completed(http.perform(get("/api/accounts/registry/" + name).param("id", id)
                .with(jwt().jwt(token -> token.claim("department", "Finance Operations"))))).andExpect(status().isForbidden());
        }
        var failure = assertThrows(ServletException.class, () -> completed(http.perform(get("/api/accounts/registry/account-snapshot").param("id", id)
            .with(jwt().jwt(token -> token.claim("department", "Finance Operations"))))));
        Throwable root = failure;
        while (root.getCause() != null) root = root.getCause();
        assertEquals(UnauthorizedMessageException.class, root.getClass());
        http.perform(get("/api/accounts/registry/account-snapshot").param("id", id)).andExpect(status().isUnauthorized());
    }

    private static JwtAuthenticationToken caller(String department) {
        return new JwtAuthenticationToken(Jwt.withTokenValue("test").header("alg", "none").subject("writer").claim("department", department).build());
    }

    private ResultActions completed(ResultActions result) throws Exception {
        var response = result.andReturn();
        return response.getRequest().isAsyncStarted() ? http.perform(asyncDispatch(response)) : result;
    }
}
