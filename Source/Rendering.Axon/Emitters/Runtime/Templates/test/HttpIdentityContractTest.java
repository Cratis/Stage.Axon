package __RUNTIME__;

import io.cratis.stage.axon.runtime.SseBridge;
import java.nio.charset.StandardCharsets;
import java.util.Base64;
import java.util.List;
import java.util.concurrent.SubmissionPublisher;
import java.util.concurrent.atomic.AtomicInteger;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.context.annotation.Import;
import org.springframework.http.MediaType;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.MvcResult;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RestController;
import org.springframework.web.servlet.mvc.method.annotation.SseEmitter;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.options;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.header;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.request;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

@SpringBootTest
@AutoConfigureMockMvc
@Import(HttpIdentityContractTest.Endpoints.class)
class HttpIdentityContractTest {
    static final AtomicInteger INVOCATIONS = new AtomicInteger();
    @Autowired private MockMvc http;

    @BeforeEach
    void reset() { INVOCATIONS.set(0); }

    @Test
    void urlTokensAreNeverAccepted() throws Exception {
        var basic = "Basic " + Base64.getEncoder().encodeToString("alice:secret".getBytes(StandardCharsets.UTF_8));
        for (var route : List.of("/_identity/json", "/_identity/stream")) {
            for (var parameter : List.of("access_token", "authorization")) {
                var result = http.perform(get(route).accept(route.endsWith("stream") ? MediaType.TEXT_EVENT_STREAM : MediaType.APPLICATION_JSON)
                    .queryParam(parameter, parameter.equals("authorization") ? basic : "x"))
                    .andExpect(status().isUnauthorized()).andExpect(request().asyncNotStarted()).andReturn();
                assertNotEventStream(result);
            }
        }
        assertEquals(0, INVOCATIONS.get());
    }

    @Test
    void crossOriginPreflightsAreDeniedByDefault() throws Exception {
        http.perform(options("/_identity/command").header("Origin", "https://evil.example")
            .header("Access-Control-Request-Method", "POST").header("Access-Control-Request-Headers", "authorization,content-type"))
            .andExpect(status().isForbidden()).andExpect(header().doesNotExist("Access-Control-Allow-Origin"));
    }

    static void assertNotEventStream(MvcResult result) {
        var type = result.getResponse().getContentType();
        assertTrue(type == null || !type.startsWith("text/event-stream"));
    }

    static boolean awaitContent(MvcResult result, String text) throws Exception {
        var deadline = System.nanoTime() + 10_000_000_000L;
        while (System.nanoTime() < deadline) {
            if (result.getResponse().getContentAsString().contains(text)) return true;
            Thread.sleep(50);
        }
        return false;
    }

    @RestController
    static class Endpoints {
        @GetMapping("/_identity/json")
        @PreAuthorize("hasRole('CONTRACT')")
        String json() { INVOCATIONS.incrementAndGet(); return "ok"; }
        @GetMapping(value = "/_identity/stream", produces = MediaType.TEXT_EVENT_STREAM_VALUE)
        @PreAuthorize("hasRole('CONTRACT')")
        SseEmitter stream() {
            INVOCATIONS.incrementAndGet();
            var publisher = new SubmissionPublisher<String>();
            var emitter = SseBridge.stream(publisher);
            publisher.submit("one");
            return emitter;
        }
        @PostMapping("/_identity/command")
        @PreAuthorize("hasRole('CONTRACT')")
        void command(@RequestBody String body) { INVOCATIONS.incrementAndGet(); }
    }
}
