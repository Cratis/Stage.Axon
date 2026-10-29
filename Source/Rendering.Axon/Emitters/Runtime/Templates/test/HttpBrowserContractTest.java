package __RUNTIME__;

import java.util.List;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.context.annotation.Import;
import org.springframework.http.MediaType;
import org.springframework.security.core.userdetails.User;
import org.springframework.security.core.userdetails.UserDetailsService;
import org.springframework.security.provisioning.InMemoryUserDetailsManager;
import org.springframework.test.context.bean.override.convention.TestBean;
import org.springframework.test.web.servlet.MockMvc;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.httpBasic;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.user;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.options;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.header;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.request;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

@SpringBootTest(properties = "stage.axon.cors.allowed-origins=https://app.example, https://other.example")
@AutoConfigureMockMvc
@Import(HttpIdentityContractTest.Endpoints.class)
class HttpBrowserContractTest {
    private static final String ALLOWED = "https://app.example";
    private static final String DENIED = "https://evil.example";
    @Autowired private MockMvc http;
    @TestBean(name = "users", methodName = "users") private UserDetailsService users;

    static UserDetailsService users() {
        return new InMemoryUserDetailsManager(User.withUsername("alice").password("{noop}secret").roles("CONTRACT").build());
    }

    @BeforeEach
    void reset() { HttpIdentityContractTest.INVOCATIONS.set(0); }

    @Test
    void listedOriginsAreTrimmedAndAllowedForQueriesAndCommands() throws Exception {
        for (var origin : List.of(ALLOWED, "https://other.example")) {
            for (var route : List.of("json", "command")) {
                http.perform(options("/_identity/" + route).header("Origin", origin)
                    .header("Access-Control-Request-Method", route.equals("command") ? "POST" : "GET")
                    .header("Access-Control-Request-Headers", "authorization,content-type"))
                    .andExpect(status().isOk()).andExpect(header().string("Access-Control-Allow-Origin", origin))
                    .andExpect(header().doesNotExist("Access-Control-Allow-Credentials"));
            }
        }
    }

    @Test
    void aDisallowedOriginIsDenied() throws Exception {
        for (var route : List.of("json", "command")) {
            http.perform(options("/_identity/" + route).header("Origin", DENIED)
                .header("Access-Control-Request-Method", route.equals("command") ? "POST" : "GET")
                .header("Access-Control-Request-Headers", "authorization,content-type"))
                .andExpect(status().isForbidden()).andExpect(header().doesNotExist("Access-Control-Allow-Origin"));
        }
        http.perform(get("/_identity/json").header("Origin", DENIED).with(user("alice").roles("CONTRACT")))
            .andExpect(status().isForbidden()).andExpect(header().doesNotExist("Access-Control-Allow-Origin"));
        assertEquals(0, HttpIdentityContractTest.INVOCATIONS.get());
    }

    @Test
    void aDisallowedOriginCannotInvokeACommand() throws Exception {
        http.perform(post("/_identity/command").contentType(MediaType.APPLICATION_JSON).content("{}")
            .header("Origin", DENIED).with(user("alice").roles("CONTRACT")))
            .andExpect(status().isForbidden()).andExpect(request().asyncNotStarted())
            .andExpect(header().doesNotExist("Access-Control-Allow-Origin"));
        assertEquals(0, HttpIdentityContractTest.INVOCATIONS.get());
    }

    @Test
    void theEventStreamWorksWithAnAuthorizationHeader() throws Exception {
        var result = http.perform(get("/_identity/stream").accept(MediaType.TEXT_EVENT_STREAM).header("Origin", ALLOWED)
            .with(httpBasic("alice", "secret"))).andExpect(request().asyncStarted())
            .andExpect(header().string("Access-Control-Allow-Origin", ALLOWED))
            .andExpect(header().doesNotExist("Access-Control-Allow-Credentials")).andReturn();
        assertTrue(result.getResponse().getContentType().startsWith("text/event-stream"));
        assertTrue(HttpIdentityContractTest.awaitContent(result, "data:one"));
    }
}
