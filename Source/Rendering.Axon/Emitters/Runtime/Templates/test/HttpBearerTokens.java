package __RUNTIME__;

import com.nimbusds.jose.JOSEException;
import com.nimbusds.jose.jwk.JWKSet;
import com.nimbusds.jose.jwk.RSAKey;
import com.nimbusds.jose.jwk.gen.RSAKeyGenerator;
import com.nimbusds.jose.jwk.source.ImmutableJWKSet;
import com.sun.net.httpserver.HttpServer;
import java.io.IOException;
import java.net.InetSocketAddress;
import java.nio.charset.StandardCharsets;
import java.time.Instant;
import java.util.List;
import java.util.function.Consumer;
import org.springframework.security.oauth2.jose.jws.SignatureAlgorithm;
import org.springframework.security.oauth2.jwt.JwsHeader;
import org.springframework.security.oauth2.jwt.JwtClaimsSet;
import org.springframework.security.oauth2.jwt.JwtEncoderParameters;
import org.springframework.security.oauth2.jwt.NimbusJwtEncoder;
import org.springframework.test.context.DynamicPropertyRegistry;

final class HttpBearerTokens {
    private static final String ISSUER = "https://issuer.example";
    private static final String AUDIENCE = "stage-axon-contract";
    private static final RSAKey KEY = key();
    private static final RSAKey FOREIGN = key();
    private HttpBearerTokens() {}

    private static final class Jwks {
        private static final HttpServer SERVER = start();
        private static HttpServer start() {
            try {
                var server = HttpServer.create(new InetSocketAddress("127.0.0.1", 0), 0);
                var body = new JWKSet(KEY.toPublicJWK()).toString().getBytes(StandardCharsets.UTF_8);
                server.createContext("/jwks", exchange -> {
                    try (exchange) {
                        exchange.getResponseHeaders().set("Content-Type", "application/json");
                        exchange.sendResponseHeaders(200, body.length);
                        exchange.getResponseBody().write(body);
                    }
                });
                server.start();
                return server;
            } catch (IOException exception) {
                throw new IllegalStateException(exception);
            }
        }
    }

    static void register(DynamicPropertyRegistry registry) {
        registry.add("spring.security.oauth2.resourceserver.jwt.jwk-set-uri", () -> "http://127.0.0.1:" + Jwks.SERVER.getAddress().getPort() + "/jwks");
        registry.add("spring.security.oauth2.resourceserver.jwt.issuer-uri", () -> ISSUER);
        registry.add("spring.security.oauth2.resourceserver.jwt.audiences", () -> AUDIENCE);
    }

    static String token(String subject, String... roles) {
        return token(claims -> claims.subject(subject).claim("roles", List.of(roles)));
    }

    static String token(Consumer<JwtClaimsSet.Builder> customize) {
        var claims = claims();
        customize.accept(claims);
        return sign(KEY, claims.build());
    }

    static String foreign() { return sign(FOREIGN, claims().build()); }

    private static JwtClaimsSet.Builder claims() {
        var now = Instant.now();
        return JwtClaimsSet.builder().issuer(ISSUER).audience(List.of(AUDIENCE)).subject("alice")
            .claim("roles", List.of("CONTRACT")).issuedAt(now).expiresAt(now.plusSeconds(300));
    }

    private static String sign(RSAKey key, JwtClaimsSet claims) {
        var encoder = new NimbusJwtEncoder(new ImmutableJWKSet<>(new JWKSet(key)));
        var header = JwsHeader.with(SignatureAlgorithm.RS256).keyId("contract").build();
        return encoder.encode(JwtEncoderParameters.from(header, claims)).getTokenValue();
    }

    private static RSAKey key() {
        try {
            return new RSAKeyGenerator(2048).keyID("contract").generate();
        } catch (JOSEException exception) {
            throw new IllegalStateException(exception);
        }
    }
}
