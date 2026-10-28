package __RUNTIME__;

import java.util.UUID;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.web.servlet.MockMvc;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.asyncDispatch;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.request;

@SpringBootTest(properties = "stage.axon.demo.claims.department=Finance Operations")
@AutoConfigureMockMvc
@ActiveProfiles("demo")
class GeneratedInlinePolicyDemoTest {
    @Autowired private MockMvc http;

    @Test
    void explicitDemoClaimsReachTheInlinePolicy() throws Exception {
        var accepted = http.perform(post("/api/accounts/registry/register-account").contentType("application/json")
                .content("{\"id\":\"" + UUID.randomUUID() + "\",\"alias\":\"demo\"}"))
                .andExpect(request().asyncStarted()).andReturn();
        http.perform(asyncDispatch(accepted)).andExpect(status().is2xxSuccessful());
    }
}

@SpringBootTest
@AutoConfigureMockMvc
@ActiveProfiles("demo")
class GeneratedInlinePolicyMissingClaimsDemoTest {
    @Autowired private MockMvc http;

    @Test
    void demoWithoutDepartmentDoesNotGrant() throws Exception {
        http.perform(post("/api/accounts/registry/register-account").contentType("application/json")
                .content("{\"id\":\"" + UUID.randomUUID() + "\",\"alias\":\"demo\"}"))
                .andExpect(status().is4xxClientError());
    }
}
