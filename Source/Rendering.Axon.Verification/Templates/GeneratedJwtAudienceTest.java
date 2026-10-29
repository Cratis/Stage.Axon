package __RUNTIME__;

import com.nimbusds.jose.JWSAlgorithm;
import com.nimbusds.jose.JWSHeader;
import com.nimbusds.jose.crypto.RSASSASigner;
import com.nimbusds.jwt.JWTClaimsSet;
import com.nimbusds.jwt.SignedJWT;
import java.nio.file.Files;
import java.nio.file.Path;
import java.security.KeyPair;
import java.security.KeyPairGenerator;
import java.time.Instant;
import java.util.Base64;
import java.util.Date;
import java.util.UUID;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.core.env.Environment;
import org.springframework.security.oauth2.jwt.JwtDecoder;
import org.springframework.security.oauth2.jwt.JwtValidationException;
import org.springframework.security.oauth2.jwt.NimbusJwtDecoder;
import org.springframework.test.context.DynamicPropertyRegistry;
import org.springframework.test.context.DynamicPropertySource;
import org.springframework.test.web.servlet.MockMvc;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

/** Exercises Boot's real decoder and the generated security chain, with a local signing fixture only. */
@SpringBootTest(properties = "spring.security.oauth2.resourceserver.jwt.audiences=stage-tests")
@AutoConfigureMockMvc
class GeneratedJwtAudienceTest {
    @Autowired private JwtDecoder decoder;
    @Autowired private MockMvc http;

    @DynamicPropertySource
    static void provider(DynamicPropertyRegistry registry) throws Exception {
        Tokens.provider(registry);
    }

    @Test
    void wrongAudienceIsRejectedByTheRealDecoderAndApplication() throws Exception {
        assertInstanceOf(NimbusJwtDecoder.class, decoder);
        var token = Tokens.token("another-application");
        assertThrows(JwtValidationException.class, () -> decoder.decode(token));
        http.perform(get("/api/accounts/registry/account-snapshot").param("id", UUID.randomUUID().toString())
            .header("Authorization", "Bearer " + token)).andExpect(status().isUnauthorized());
    }

    @Test
    void rightAudienceIsAuthenticatedBeforeTheNativePolicyDenial() throws Exception {
        assertInstanceOf(NimbusJwtDecoder.class, decoder);
        var token = Tokens.token("stage-tests");
        assertEquals("writer", decoder.decode(token).getSubject());
        http.perform(get("/api/accounts/registry/account-snapshot").param("id", UUID.randomUUID().toString())
            .header("Authorization", "Bearer " + token)).andExpect(status().isForbidden());
    }

    static final class Tokens {
        private static final KeyPair KEY = keys();

        private Tokens() { }

        private static KeyPair keys() {
            try {
                var generator = KeyPairGenerator.getInstance("RSA");
                generator.initialize(2048);
                return generator.generateKeyPair();
            } catch (Exception failure) {
                throw new ExceptionInInitializerError(failure);
            }
        }

        static void provider(DynamicPropertyRegistry registry) throws Exception {
            var path = Path.of("target", "jwt-audience-public-key.pem").toAbsolutePath();
            Files.createDirectories(path.getParent());
            var publicKey = Base64.getMimeEncoder(64, new byte[] {'\n'}).encodeToString(KEY.getPublic().getEncoded());
            Files.writeString(path, "-----BEGIN PUBLIC KEY-----\n" + publicKey + "\n-----END PUBLIC KEY-----\n");
            registry.add("spring.security.oauth2.resourceserver.jwt.public-key-location", () -> path.toUri().toString());
        }

        static String token(String audience) throws Exception {
            var now = Instant.now();
            var claims = new JWTClaimsSet.Builder().subject("writer").audience(audience)
                .claim("department", "Other").issueTime(Date.from(now)).expirationTime(Date.from(now.plusSeconds(300))).build();
            var jwt = new SignedJWT(new JWSHeader(JWSAlgorithm.RS256), claims);
            jwt.sign(new RSASSASigner(KEY.getPrivate()));
            return jwt.serialize();
        }
    }
}

/** No audience property: even a correctly signed token accepted by Nimbus must not enter the application. */
@SpringBootTest
@AutoConfigureMockMvc
class GeneratedJwtNoAudienceTest {
    @Autowired private JwtDecoder decoder;
    @Autowired private MockMvc http;
    @Autowired private Environment environment;

    @DynamicPropertySource
    static void provider(DynamicPropertyRegistry registry) throws Exception {
        GeneratedJwtAudienceTest.Tokens.provider(registry);
    }

    @Test
    void noConfiguredAudienceRejectsAnOtherwiseValidToken() throws Exception {
        assertFalse(environment.containsProperty("spring.security.oauth2.resourceserver.jwt.audiences"));
        assertInstanceOf(NimbusJwtDecoder.class, decoder);
        var token = GeneratedJwtAudienceTest.Tokens.token("stage-tests");
        assertEquals("writer", decoder.decode(token).getSubject());
        http.perform(get("/api/accounts/registry/account-snapshot").param("id", UUID.randomUUID().toString())
            .header("Authorization", "Bearer " + token)).andExpect(status().isUnauthorized());
    }
}
