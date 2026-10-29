package __RUNTIME__;

import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.context.annotation.Import;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RestController;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.anonymous;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.content;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

@SpringBootTest
@AutoConfigureMockMvc
@Import(HttpPublicContractTest.Endpoints.class)
class HttpPublicContractTest {
    @Autowired private MockMvc http;

    @Test
    void publicEndpointsStayAnonymousWithoutAJwtProvider() throws Exception {
        http.perform(get("/_contract/public").with(anonymous())).andExpect(status().isOk()).andExpect(content().string("public"));
    }

    @RestController
    static class Endpoints {
        @GetMapping("/_contract/public")
        @PreAuthorize("permitAll()")
        String publicEndpoint() { return "public"; }
    }
}
