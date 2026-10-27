package __RUNTIME__;

import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.context.annotation.Import;
import org.springframework.test.web.servlet.MockMvc;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

@SpringBootTest
@AutoConfigureMockMvc
@Import(HttpDemoIdentityContractTest.EndpointsConfiguration.class)
class HttpDemoIdentityDisabledTest {
    @Autowired private MockMvc http;

    @Test
    void requiresCredentialsWithoutTheDemoProfile() throws Exception {
        for (var endpoint : new String[] { "authenticated", "authorities", "role", "denied" }) {
            http.perform(get("/__demo-identity/" + endpoint)).andExpect(status().isUnauthorized());
        }
    }
}
