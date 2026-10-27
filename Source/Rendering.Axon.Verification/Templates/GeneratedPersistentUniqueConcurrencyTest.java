package __RUNTIME__;

import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import org.axonframework.common.configuration.Configuration;
import org.axonframework.messaging.commandhandling.gateway.CommandGateway;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.test.annotation.DirtiesContext;
import org.springframework.test.context.ActiveProfiles;

@SpringBootTest(webEnvironment = SpringBootTest.WebEnvironment.NONE, properties = {
        "spring.datasource.url=jdbc:h2:mem:generated-unique;DB_CLOSE_DELAY=-1;LOCK_TIMEOUT=5000",
        "spring.jpa.hibernate.ddl-auto=create-drop"})
@ActiveProfiles("persistent")
@DirtiesContext(classMode = DirtiesContext.ClassMode.AFTER_CLASS)
class GeneratedPersistentUniqueConcurrencyTest extends PersistentUniqueConcurrencyTest {
    @Autowired private CommandGateway gateway;
    @Autowired private Configuration configuration;

    @Override
    protected CompletableFuture<Void> register(UUID owner, String value) {
        return GeneratedIngressSupport.register(gateway, owner, value);
    }

    @Override
    protected CompletableFuture<Void> release(UUID owner) {
        return GeneratedIngressSupport.release(gateway, owner);
    }

    @Override
    protected long events(UUID owner) throws Exception {
        return GeneratedIngressSupport.facts(configuration, owner).size();
    }
}
