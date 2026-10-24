// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

package __RUNTIME__;

import java.util.UUID;
import java.util.concurrent.atomic.AtomicReference;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.context.annotation.Import;
import org.springframework.http.MediaType;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RestController;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.user;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

@SpringBootTest
@AutoConfigureMockMvc
@Import(RuntimeHttpRoundTripTest.EchoEndpoint.class)
class RuntimeHttpRoundTripTest {
    @Autowired
    private MockMvc http;

    public record Id(UUID value) implements ConceptAs<UUID> {}
    public record Payload(Id id) {}

    @Test
    void conceptIdsStayPrimitiveAcrossHttpAndStorage() throws Exception {
        var id = "00000000-0000-0000-0000-000000000001";
        http.perform(post("/_runtime/echo").contentType(MediaType.APPLICATION_JSON).content("{\"id\":\"" + id + "\"}"))
            .andExpect(status().isOk()).andExpect(jsonPath("$.id").value(id));
        http.perform(get("/_runtime/echo")).andExpect(status().isOk()).andExpect(jsonPath("$.id").value(id));
    }

    @Test
    void validationErrorsUseProblemDetails() throws Exception {
        http.perform(get("/_runtime/invalid")).andExpect(status().isBadRequest())
            .andExpect(jsonPath("$.status").value(400)).andExpect(jsonPath("$.errors[0].property").value("id"));
    }

    @Test
    void methodAuthorizationFailsClosed() throws Exception {
        http.perform(get("/_runtime/admin")).andExpect(status().isUnauthorized());
        http.perform(get("/_runtime/admin").with(user("reader").roles("READER"))).andExpect(status().isForbidden());
        http.perform(get("/_runtime/admin").with(user("admin").roles("ADMIN"))).andExpect(status().isOk());
    }

    @RestController
    static class EchoEndpoint {
        private final AtomicReference<Payload> stored = new AtomicReference<>();

        @PostMapping("/_runtime/echo")
        Payload echo(@RequestBody Payload payload) { stored.set(payload); return payload; }

        @GetMapping("/_runtime/echo")
        Payload read() { return stored.get(); }

        @GetMapping("/_runtime/invalid")
        Payload invalid() { throw new CommandValidationException(java.util.List.of(new ValidationError("id", "Required"))); }

        @GetMapping("/_runtime/admin")
        @PreAuthorize("hasRole('ADMIN')")
        String admin() { return "allowed"; }
    }
}
