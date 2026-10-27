package __RUNTIME__;

import java.util.stream.Collectors;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Import;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.security.core.Authentication;
import org.springframework.security.core.GrantedAuthority;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RestController;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.springframework.security.core.context.SecurityContextHolder.getContext;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.content;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

@SpringBootTest
@AutoConfigureMockMvc
@ActiveProfiles("demo")
@Import(HttpDemoIdentityContractTest.EndpointsConfiguration.class)
class HttpDemoIdentityContractTest {
    @Autowired private MockMvc http;

    @Test
    void authenticatesWithoutCredentialsAndIgnoresSuppliedCredentials() throws Exception {
        for (var credential : new String[] { "", "Basic invalid", "Bearer invalid" }) {
            http.perform(get("/__demo-identity/authenticated").header("Authorization", credential))
                .andExpect(status().isOk()).andExpect(content().string("demo"));
            assertNull(getContext().getAuthentication());
        }
    }

    @Test
    void grantsExactlyTheModeledRoles() throws Exception {
        http.perform(get("/__demo-identity/authorities"))
            .andExpect(status().isOk()).andExpect(content().string(__DEMO_AUTHORITY_TEXT__));
        if (!__DEMO_AUTHORITY_TEXT__.isEmpty()) {
            http.perform(get("/__demo-identity/role"))
                .andExpect(status().isOk()).andExpect(content().string("demo"));
        }
    }

    @Test
    void stillEnforcesDenyAll() throws Exception {
        http.perform(get("/__demo-identity/denied")).andExpect(status().isForbidden());
    }

    @TestConfiguration
    static class EndpointsConfiguration {
        @Bean Endpoints demoIdentityEndpoints() { return new Endpoints(); }
    }

    @RestController
    static class Endpoints {
        @GetMapping("/__demo-identity/authenticated")
        @PreAuthorize("isAuthenticated()")
        String authenticated(Authentication caller) { return caller.getName(); }

        @GetMapping("/__demo-identity/authorities")
        @PreAuthorize("isAuthenticated()")
        String authorities(Authentication caller) {
            return caller.getAuthorities().stream().map(GrantedAuthority::getAuthority).sorted().collect(Collectors.joining(","));
        }

        @GetMapping("/__demo-identity/role")
        @PreAuthorize(__DEMO_ROLE_POLICY__)
        String role(Authentication caller) { return caller.getName(); }

        @GetMapping("/__demo-identity/denied")
        @PreAuthorize("denyAll()")
        String denied() { return "unreachable"; }
    }
}
