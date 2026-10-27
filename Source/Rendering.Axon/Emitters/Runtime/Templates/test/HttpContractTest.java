package __RUNTIME__;

import java.util.List;
import java.util.concurrent.CompletableFuture;
import org.axonframework.messaging.commandhandling.annotation.Command;
import org.axonframework.messaging.commandhandling.annotation.CommandHandler;
import org.axonframework.messaging.commandhandling.gateway.CommandGateway;
import org.axonframework.messaging.queryhandling.annotation.QueryHandler;
import org.axonframework.messaging.queryhandling.gateway.QueryGateway;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.context.annotation.Import;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
import org.springframework.stereotype.Component;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.ResultActions;
import org.springframework.test.web.servlet.ResultMatcher;
import org.springframework.test.web.servlet.request.MockHttpServletRequestBuilder;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;
import static org.hamcrest.Matchers.containsString;
import static org.hamcrest.Matchers.not;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.asyncDispatch;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.content;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

@SpringBootTest
@AutoConfigureMockMvc
@Import({HttpContractTest.Handlers.class, HttpContractTest.Endpoints.class})
class HttpContractTest {
    private static final String PROBLEM = "urn:cratis:stage-axon:problem:";
    private static volatile List<Reply> rows = List.of();

    @Autowired
    private MockMvc http;

    @Command(name = "HttpContractSucceed")
    public record Succeed(String id) {}
    @Command(name = "HttpContractRespond")
    public record Respond(String id) {}
    public record Reply(String id) {}
    @Command(name = "HttpContractFailValidation")
    public record FailValidation(String id) {}
    @Command(name = "HttpContractFailConflict")
    public record FailConflict(String id) {}
    @Command(name = "HttpContractFailInvalid")
    public record FailInvalid(String id) {}
    @Command(name = "HttpContractFailUnsupported")
    public record FailUnsupported(String id) {}
    @Command(name = "HttpContractFailCardinality")
    public record FailCardinality(String id) {}

    public record ExactlyOne() {}
    public record ExactlyOneResult(List<Reply> rows) {}
    public record AtMostOne() {}
    public record AtMostOneResult(List<Reply> rows) {}
    public record Many() {}
    public record ManyResult(List<Reply> rows) {}

    @Test
    void voidCommandsAnswerOkWithAnEmptyBody() throws Exception {
        perform(command("/_contract/succeed")).andExpect(status().isOk()).andExpect(content().string(""));
    }

    @Test
    void recordResponsesAnswerOkWithAJsonObject() throws Exception {
        perform(command("/_contract/respond")).andExpect(status().isOk())
            .andExpect(content().contentTypeCompatibleWith(MediaType.APPLICATION_JSON)).andExpect(jsonPath("$.id").value("1"));
    }

    @Test
    void asynchronousValidationFailuresAreBadRequestsWithErrors() throws Exception {
        perform(command("/_contract/fail-validation")).andExpect(status().isBadRequest()).andExpect(problem("validation"))
            .andExpect(jsonPath("$.errors[0].property").value("id"));
    }

    @Test
    void synchronousValidationFailuresMatchAsynchronousOnes() throws Exception {
        perform(command("/_contract/sync-validation")).andExpect(status().isBadRequest()).andExpect(problem("validation"))
            .andExpect(jsonPath("$.errors[0].property").value("id"));
    }

    @Test
    void asynchronousConflictsAreConflicts() throws Exception {
        perform(command("/_contract/fail-conflict")).andExpect(status().isConflict()).andExpect(problem("conflict"));
    }

    @Test
    void asynchronousInvalidArgumentsAreBadRequests() throws Exception {
        perform(command("/_contract/fail-invalid")).andExpect(status().isBadRequest()).andExpect(problem("invalid-argument"));
    }

    @Test
    void asynchronousUnsupportedOperationsAreNotImplemented() throws Exception {
        perform(command("/_contract/fail-unsupported")).andExpect(status().isNotImplemented()).andExpect(problem("unsupported"));
    }

    @Test
    void asynchronousCardinalityFailuresAreMissingResults() throws Exception {
        perform(command("/_contract/fail-cardinality")).andExpect(status().isNotFound()).andExpect(problem("query-result-missing"));
    }

    @Test
    void anAbsentOptionalResultIsAnEmptyNotFound() throws Exception {
        rows = List.of();
        perform(get("/_contract/at-most-one")).andExpect(status().isNotFound()).andExpect(content().string(""));
    }

    @Test
    void aPresentOptionalResultIsReturned() throws Exception {
        rows = List.of(new Reply("a"));
        perform(get("/_contract/at-most-one")).andExpect(status().isOk()).andExpect(jsonPath("$.id").value("a"));
    }

    @Test
    void moreThanOneOptionalResultIsAServerError() throws Exception {
        rows = List.of(new Reply("a"), new Reply("b"));
        perform(get("/_contract/at-most-one")).andExpect(status().isInternalServerError()).andExpect(problem("query-cardinality"))
            .andExpect(jsonPath("$.count").value(2)).andExpect(content().string(not(containsString("\"a\""))));
    }

    @Test
    void anAbsentRequiredResultIsAMissingResultProblem() throws Exception {
        rows = List.of();
        perform(get("/_contract/exactly-one")).andExpect(status().isNotFound()).andExpect(problem("query-result-missing"));
    }

    @Test
    void moreThanOneRequiredResultIsAServerError() throws Exception {
        rows = List.of(new Reply("a"), new Reply("b"));
        perform(get("/_contract/exactly-one")).andExpect(status().isInternalServerError()).andExpect(problem("query-cardinality"));
    }

    @Test
    void aSingleRequiredResultIsReturned() throws Exception {
        rows = List.of(new Reply("a"));
        perform(get("/_contract/exactly-one")).andExpect(status().isOk()).andExpect(jsonPath("$.id").value("a"));
    }

    @Test
    void manyResultsAreAJsonArray() throws Exception {
        rows = List.of(new Reply("a"), new Reply("b"));
        perform(get("/_contract/many")).andExpect(status().isOk()).andExpect(jsonPath("$.length()").value(2));
        rows = List.of();
        perform(get("/_contract/many")).andExpect(status().isOk()).andExpect(content().json("[]"));
    }

    @Test
    void malformedJsonIsABadRequestProblem() throws Exception {
        perform(post("/_contract/succeed").contentType(MediaType.APPLICATION_JSON).content("{"))
            .andExpect(status().isBadRequest()).andExpect(content().contentTypeCompatibleWith(MediaType.APPLICATION_PROBLEM_JSON));
    }

    @Test
    void aMissingRequiredQueryParameterIsABadRequestProblem() throws Exception {
        perform(get("/_contract/required")).andExpect(status().isBadRequest())
            .andExpect(content().contentTypeCompatibleWith(MediaType.APPLICATION_PROBLEM_JSON));
    }

    @Test
    void aNonJsonCommandBodyIsUnsupportedMediaType() throws Exception {
        perform(post("/_contract/succeed").contentType(MediaType.TEXT_PLAIN).content("id=1"))
            .andExpect(status().isUnsupportedMediaType()).andExpect(content().contentTypeCompatibleWith(MediaType.APPLICATION_PROBLEM_JSON));
    }

    private static MockHttpServletRequestBuilder command(String path) {
        return post(path).contentType(MediaType.APPLICATION_JSON).content("{\"id\":\"1\"}");
    }

    private static ResultMatcher problem(String type) {
        return result -> {
            content().contentTypeCompatibleWith(MediaType.APPLICATION_PROBLEM_JSON).match(result);
            jsonPath("$.type").value(PROBLEM + type).match(result);
        };
    }

    private ResultActions perform(MockHttpServletRequestBuilder request) throws Exception {
        var actions = http.perform(request);
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
        void handle(Succeed command) {}

        @CommandHandler
        Reply handle(Respond command) { return new Reply(command.id()); }

        @CommandHandler
        void handle(FailValidation command) { throw new CommandValidationException(List.of(new ValidationError("id", "Required"))); }

        @CommandHandler
        void handle(FailConflict command) { throw new UniqueValueConflict("Duplicate"); }

        @CommandHandler
        void handle(FailInvalid command) { throw new IllegalArgumentException("Invalid"); }

        @CommandHandler
        void handle(FailUnsupported command) { throw new UnsupportedOperationException("Unsupported"); }

        @CommandHandler
        void handle(FailCardinality command) { QueryResults.exactlyOne(List.<Reply>of()); }

        @QueryHandler
        ExactlyOneResult handle(ExactlyOne query) { return new ExactlyOneResult(QueryResults.exactlyOne(List.copyOf(rows))); }

        @QueryHandler
        AtMostOneResult handle(AtMostOne query) { return new AtMostOneResult(QueryResults.atMostOne(List.copyOf(rows))); }

        @QueryHandler
        ManyResult handle(Many query) { return new ManyResult(List.copyOf(rows)); }
    }

    @RestController
    static class Endpoints {
        private final CommandGateway commands;
        private final QueryGateway queries;

        Endpoints(CommandGateway commands, QueryGateway queries) {
            this.commands = commands;
            this.queries = queries;
        }

        @PostMapping("/_contract/succeed")
        CompletableFuture<Void> succeed(@RequestBody Succeed command) { return commands.send(command, Void.class); }

        @PostMapping("/_contract/respond")
        CompletableFuture<Reply> respond(@RequestBody Respond command) { return commands.send(command, Reply.class); }

        @PostMapping("/_contract/fail-validation")
        CompletableFuture<Void> failValidation(@RequestBody FailValidation command) { return commands.send(command, Void.class); }

        @PostMapping("/_contract/fail-conflict")
        CompletableFuture<Void> failConflict(@RequestBody FailConflict command) { return commands.send(command, Void.class); }

        @PostMapping("/_contract/fail-invalid")
        CompletableFuture<Void> failInvalid(@RequestBody FailInvalid command) { return commands.send(command, Void.class); }

        @PostMapping("/_contract/fail-unsupported")
        CompletableFuture<Void> failUnsupported(@RequestBody FailUnsupported command) { return commands.send(command, Void.class); }

        @PostMapping("/_contract/fail-cardinality")
        CompletableFuture<Void> failCardinality(@RequestBody FailCardinality command) { return commands.send(command, Void.class); }

        @PostMapping("/_contract/sync-validation")
        CompletableFuture<Void> syncValidation(@RequestBody Succeed command) {
            throw new CommandValidationException(List.of(new ValidationError("id", "Required")));
        }

        @GetMapping("/_contract/exactly-one")
        CompletableFuture<ResponseEntity<Reply>> exactlyOne() {
            return queries.query(new ExactlyOne(), ExactlyOneResult.class).thenApply(result -> ResponseEntity.ok(result.rows().getFirst()));
        }

        @GetMapping("/_contract/at-most-one")
        CompletableFuture<ResponseEntity<Reply>> atMostOne() {
            return queries.query(new AtMostOne(), AtMostOneResult.class)
                .thenApply(result -> result.rows().isEmpty() ? ResponseEntity.notFound().build() : ResponseEntity.ok(result.rows().getFirst()));
        }

        @GetMapping("/_contract/many")
        CompletableFuture<ResponseEntity<List<Reply>>> many() {
            return queries.query(new Many(), ManyResult.class).thenApply(result -> ResponseEntity.ok(result.rows()));
        }

        @GetMapping("/_contract/required")
        String required(@RequestParam String name) { return name; }
    }
}
