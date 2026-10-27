package __RUNTIME__;

import com.jayway.jsonpath.JsonPath;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.time.Duration;
import java.util.UUID;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.Timeout;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.test.web.server.LocalServerPort;
import org.springframework.test.annotation.DirtiesContext;
import org.springframework.test.context.ActiveProfiles;
import static org.awaitility.Awaitility.await;
import static org.junit.jupiter.api.Assertions.assertEquals;

@SpringBootTest(webEnvironment = SpringBootTest.WebEnvironment.RANDOM_PORT)
@ActiveProfiles("demo")
@DirtiesContext(classMode = DirtiesContext.ClassMode.AFTER_CLASS)
@Timeout(60)
class GeneratedDemoIdentityTest {
    @LocalServerPort private int port;

    @Test
    void executesAModeledCommandAndReadsItsProjectionWithoutCredentials() throws Exception {
        var id = UUID.randomUUID();
        var alias = UUID.randomUUID().toString();
        var body = "{\"id\":\"" + id + "\",\"alias\":\"" + alias + "\",\"actor\":\"forged\"}";
        try (var client = HttpClient.newHttpClient()) {
            var command = HttpRequest.newBuilder(uri("/api/accounts/registry/register-account"))
                .timeout(Duration.ofSeconds(10)).header("Content-Type", "application/json")
                .POST(HttpRequest.BodyPublishers.ofString(body)).build();
            var accepted = client.send(command, HttpResponse.BodyHandlers.ofString());
            assertEquals(200, accepted.statusCode(), accepted.body());
            var query = HttpRequest.newBuilder(uri("/api/accounts/registry/account-snapshot?id=" + id))
                .timeout(Duration.ofSeconds(10)).GET().build();
            await().atMost(Duration.ofSeconds(10)).untilAsserted(() -> {
                var result = client.send(query, HttpResponse.BodyHandlers.ofString());
                assertEquals(200, result.statusCode(), result.body());
                assertEquals(alias, JsonPath.read(result.body(), "$.alias"));
                assertEquals("demo", JsonPath.read(result.body(), "$.actor"));
            });
        }
    }

    private URI uri(String path) { return URI.create("http://localhost:" + port + path); }
}
