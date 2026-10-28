package __RUNTIME__;

import java.util.UUID;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.web.servlet.MockMvc;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

@SpringBootTest
@AutoConfigureMockMvc
@ActiveProfiles("demo")
class GeneratedDemoIdentityTest {
    @Autowired private MockMvc http;

    @Test
    void demoProfileDoesNotGrantAnonymousAccessToARealCommand() throws Exception {
        http.perform(post("/api/accounts/registry/register-account").contentType("application/json")
            .content("{\"id\":\"" + UUID.randomUUID() + "\",\"alias\":\"proof\",\"actor\":\"forged\"}"))
            .andExpect(status().isUnauthorized());
    }
}
