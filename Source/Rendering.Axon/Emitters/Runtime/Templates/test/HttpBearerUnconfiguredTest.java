package __RUNTIME__;

import __SECURITY__.JwtAudienceConfiguration;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.test.context.runner.ApplicationContextRunner;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.context.annotation.Import;
import org.springframework.test.web.servlet.MockMvc;
import static org.assertj.core.api.Assertions.assertThat;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.content;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.request;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

@SpringBootTest
@AutoConfigureMockMvc
@Import({HttpBearerContractTest.Handlers.class, HttpBearerContractTest.Endpoints.class})
class HttpBearerUnconfiguredTest {
    @Autowired
    private MockMvc http;

    @ParameterizedTest
    @ValueSource(strings = { "issuer-uri", "jwk-set-uri", "public-key-location" })
    void aDecoderSourceWithoutAudiencesRefusesStartup(String source) {
        for (var audiences : new String[] { null, "", "   " }) {
            var runner = new ApplicationContextRunner().withUserConfiguration(JwtAudienceConfiguration.class)
                .withPropertyValues("spring.security.oauth2.resourceserver.jwt." + source + "=https://issuer.example/source");
            if (audiences != null) runner = runner.withPropertyValues("spring.security.oauth2.resourceserver.jwt.audiences=" + audiences);
            runner.run(context -> {
                    assertThat(context).hasFailed();
                    assertThat(context.getStartupFailure()).isInstanceOf(IllegalStateException.class)
                        .hasMessageContaining("spring.security.oauth2.resourceserver.jwt.audiences");
                });
        }
    }

    @ParameterizedTest
    @ValueSource(strings = { "issuer-uri", "jwk-set-uri", "public-key-location" })
    void aDecoderSourceWithAudiencesPassesTheGuard(String source) {
        new ApplicationContextRunner().withUserConfiguration(JwtAudienceConfiguration.class)
            .withPropertyValues("spring.security.oauth2.resourceserver.jwt." + source + "=https://issuer.example/source",
                "spring.security.oauth2.resourceserver.jwt.audiences[0]=" + HttpBearerTokens.AUDIENCE)
            .run(context -> assertThat(context).hasNotFailed());
    }

    @BeforeEach
    void reset() { HttpBearerContractTest.INVOCATIONS.set(0); }

    @Test
    void evenCorrectlySignedTokensAreRejectedWithoutAProvider() throws Exception {
        var token = HttpBearerTokens.token("alice", "CONTRACT");
        for (var endpoint : HttpBearerContractTest.protectedRequests()) {
            var result = http.perform(endpoint.header("Authorization", "Bearer " + token))
                .andExpect(status().isUnauthorized()).andExpect(request().asyncNotStarted()).andReturn();
            HttpBearerContractTest.assertNotEventStream(result);
        }
        assertEquals(0, HttpBearerContractTest.INVOCATIONS.get());
    }

    @Test
    void publicEndpointsStayAnonymous() throws Exception {
        http.perform(get("/_bearer/public")).andExpect(status().isOk()).andExpect(content().string("public"));
    }
}
