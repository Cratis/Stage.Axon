package __RUNTIME__;

import java.util.concurrent.CompletableFuture;
import java.util.concurrent.atomic.AtomicInteger;
import org.axonframework.messaging.commandhandling.annotation.Command;
import org.axonframework.messaging.commandhandling.annotation.CommandHandler;
import org.axonframework.messaging.commandhandling.gateway.CommandGateway;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.context.annotation.Import;
import org.springframework.http.MediaType;
import org.springframework.stereotype.Component;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.ResultActions;
import org.springframework.test.web.servlet.request.MockHttpServletRequestBuilder;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.user;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.asyncDispatch;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.content;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

@SpringBootTest
@AutoConfigureMockMvc
@Import({HttpErrorsContractTest.Handlers.class, HttpErrorsContractTest.Endpoints.class})
class HttpErrorsContractTest {
    private static final AtomicInteger INVOCATIONS = new AtomicInteger();
    @Autowired private MockMvc http;

    @Command(name = "HttpErrorsFailInvalid")
    public record FailInvalid(String id) {}
    @Command(name = "HttpErrorsFailUnsupported")
    public record FailUnsupported(String id) {}
    @Command(name = "HttpErrorsSucceed")
    public record Succeed(String id) {}

    @BeforeEach
    void reset() { INVOCATIONS.set(0); }

    @Test
    void asynchronousInvalidArgumentsAreBadRequests() throws Exception {
        perform(post("/_contract/fail-invalid").contentType(MediaType.APPLICATION_JSON).content("{\"id\":\"1\"}"))
            .andExpect(status().isBadRequest()).andExpect(content().contentTypeCompatibleWith(MediaType.APPLICATION_PROBLEM_JSON))
            .andExpect(jsonPath("$.type").value("urn:cratis:stage-axon:problem:invalid-argument"));
    }

    @Test
    void asynchronousUnsupportedOperationsAreNotImplemented() throws Exception {
        perform(post("/_contract/fail-unsupported").contentType(MediaType.APPLICATION_JSON).content("{\"id\":\"1\"}"))
            .andExpect(status().isNotImplemented()).andExpect(content().contentTypeCompatibleWith(MediaType.APPLICATION_PROBLEM_JSON))
            .andExpect(jsonPath("$.type").value("urn:cratis:stage-axon:problem:unsupported"));
    }

    @Test
    void malformedJsonIsABadRequestProblem() throws Exception {
        perform(post("/_contract/succeed").contentType(MediaType.APPLICATION_JSON).content("{"))
            .andExpect(status().isBadRequest()).andExpect(content().contentTypeCompatibleWith(MediaType.APPLICATION_PROBLEM_JSON));
        assertEquals(0, INVOCATIONS.get());
    }

    @Test
    void aMissingRequiredQueryParameterIsABadRequestProblem() throws Exception {
        perform(get("/_contract/required")).andExpect(status().isBadRequest())
            .andExpect(content().contentTypeCompatibleWith(MediaType.APPLICATION_PROBLEM_JSON));
    }

    @Test
    void simpleRequestContentTypesCannotReachCommands() throws Exception {
        for (var type : new MediaType[] {MediaType.TEXT_PLAIN, MediaType.APPLICATION_FORM_URLENCODED}) {
            perform(post("/_contract/succeed").contentType(type).content("id=1"))
                .andExpect(status().isUnsupportedMediaType()).andExpect(content().contentTypeCompatibleWith(MediaType.APPLICATION_PROBLEM_JSON));
        }
        assertEquals(0, INVOCATIONS.get());
    }

    private ResultActions perform(MockHttpServletRequestBuilder request) throws Exception {
        var actions = http.perform(request.with(user("alice").roles("CONTRACT")));
        var result = actions.andReturn();
        if (result.getRequest().isAsyncStarted()) {
            result.getAsyncResult(10000);
            return http.perform(asyncDispatch(result));
        }
        return actions;
    }

    @Component
    static class Handlers {
        @CommandHandler
        void handle(FailInvalid command) { throw new IllegalArgumentException("Invalid"); }
        @CommandHandler
        void handle(FailUnsupported command) { throw new UnsupportedOperationException("Unsupported"); }
        @CommandHandler
        void handle(Succeed command) { INVOCATIONS.incrementAndGet(); }
    }

    @RestController
    static class Endpoints {
        private final CommandGateway commands;
        Endpoints(CommandGateway commands) { this.commands = commands; }
        @PostMapping("/_contract/fail-invalid")
        CompletableFuture<Void> invalid(@RequestBody FailInvalid command) { return commands.send(command, Void.class); }
        @PostMapping("/_contract/fail-unsupported")
        CompletableFuture<Void> unsupported(@RequestBody FailUnsupported command) { return commands.send(command, Void.class); }
        @PostMapping("/_contract/succeed")
        CompletableFuture<Void> succeed(@RequestBody Succeed command) { return commands.send(command, Void.class); }
        @GetMapping("/_contract/required")
        String required(@RequestParam("id") String id) { return id; }
    }
}
