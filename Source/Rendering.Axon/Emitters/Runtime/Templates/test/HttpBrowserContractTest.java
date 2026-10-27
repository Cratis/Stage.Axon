package __RUNTIME__;

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
import static org.hamcrest.Matchers.startsWith;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.httpBasic;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.options;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.header;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.request;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

@SpringBootTest(properties = "stage.axon.cors.allowed-origins=https://app.example, https://other.example")
@AutoConfigureMockMvc
@Import({HttpIdentityContractTest.Handlers.class, HttpIdentityContractTest.Endpoints.class})
class HttpBrowserContractTest {
    private static final String ALLOWED = "https://app.example";
    private static final String DENIED = "https://evil.example";

    @Autowired
    private MockMvc http;

    @TestBean(name = "users", methodName = "users")
    private UserDetailsService users;

    static UserDetailsService users() {
        return new InMemoryUserDetailsManager(User.withUsername("alice").password("{noop}secret").roles("CONTRACT").build());
    }

    @Test
    void anAllowedOriginPassesThePreflight() throws Exception {
        http.perform(options("/_identity/command").header("Origin", ALLOWED).header("Access-Control-Request-Method", "POST")
            .header("Access-Control-Request-Headers", "authorization,content-type"))
            .andExpect(status().isOk()).andExpect(header().string("Access-Control-Allow-Origin", ALLOWED));
    }

    @Test
    void everyListedOriginIsTrimmedAndAllowed() throws Exception {
        http.perform(options("/_identity/json").header("Origin", "https://other.example").header("Access-Control-Request-Method", "GET"))
            .andExpect(status().isOk()).andExpect(header().string("Access-Control-Allow-Origin", "https://other.example"));
    }

    @Test
    void aDisallowedOriginIsDenied() throws Exception {
        var preflight = http.perform(options("/_identity/command").header("Origin", DENIED).header("Access-Control-Request-Method", "POST"))
            .andExpect(status().isForbidden()).andReturn();
        assertNull(preflight.getResponse().getHeader("Access-Control-Allow-Origin"));
        var actual = http.perform(get("/_identity/json").header("Origin", DENIED).with(httpBasic("alice", "secret")))
            .andExpect(status().isForbidden()).andReturn();
        assertNull(actual.getResponse().getHeader("Access-Control-Allow-Origin"));
    }

    @Test
    void aDisallowedOriginCannotInvokeACommand() throws Exception {
        HttpIdentityContractTest.INVOCATIONS.set(0);
        http.perform(post("/_identity/command").contentType(MediaType.APPLICATION_JSON).content("{\"identity\":\"alice\"}")
            .header("Origin", DENIED).with(httpBasic("alice", "secret"))).andExpect(status().isForbidden());
        assertEquals(0, HttpIdentityContractTest.INVOCATIONS.get());
    }

    @Test
    void credentialsAreNotSharedAcrossOrigins() throws Exception {
        var result = http.perform(options("/_identity/json").header("Origin", ALLOWED).header("Access-Control-Request-Method", "GET")).andReturn();
        assertNull(result.getResponse().getHeader("Access-Control-Allow-Credentials"));
    }

    @Test
    void anUnauthenticatedFetchGetsNoBasicChallenge() throws Exception {
        http.perform(get("/_identity/json").header("Origin", ALLOWED).header("Sec-Fetch-Mode", "cors"))
            .andExpect(status().isUnauthorized()).andExpect(header().doesNotExist("WWW-Authenticate"));
    }

    @Test
    void anUnauthenticatedXmlHttpRequestGetsNoBasicChallenge() throws Exception {
        http.perform(get("/_identity/json").header("X-Requested-With", "XMLHttpRequest"))
            .andExpect(status().isUnauthorized()).andExpect(header().doesNotExist("WWW-Authenticate"));
    }

    @Test
    void aNonBrowserClientStillReceivesTheChallenge() throws Exception {
        http.perform(get("/_identity/json")).andExpect(status().isUnauthorized()).andExpect(header().string("WWW-Authenticate", startsWith("Basic")));
    }

    @Test
    void theEventStreamWorksWithAnAuthorizationHeader() throws Exception {
        var result = http.perform(get("/_identity/stream").accept(MediaType.TEXT_EVENT_STREAM).header("Origin", ALLOWED).header("Sec-Fetch-Mode", "cors")
            .with(httpBasic("alice", "secret"))).andExpect(request().asyncStarted()).andExpect(header().string("Access-Control-Allow-Origin", ALLOWED)).andReturn();
        assertTrue(result.getResponse().getContentType().startsWith("text/event-stream"));
        assertTrue(HttpIdentityContractTest.awaitContent(result, "data:one"));
    }

    @Test
    void theEventStreamRejectsUrlTokens() throws Exception {
        http.perform(get("/_identity/stream").accept(MediaType.TEXT_EVENT_STREAM).queryParam("access_token", "x").header("Sec-Fetch-Mode", "cors"))
            .andExpect(status().isUnauthorized()).andExpect(header().doesNotExist("WWW-Authenticate"));
    }
}
