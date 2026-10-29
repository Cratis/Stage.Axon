package __RUNTIME__;

import io.cratis.stage.axon.runtime.persistent.PersistentConfiguration;
import io.cratis.stage.axon.runtime.persistent.UniqueClaim;
import jakarta.persistence.EntityManagerFactory;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.UUID;
import org.axonframework.eventsourcing.eventstore.jpa.AggregateEventEntry;
import org.axonframework.messaging.eventhandling.processing.streaming.token.store.jpa.TokenEntry;
import org.springframework.boot.autoconfigure.EnableAutoConfiguration;
import org.springframework.boot.builder.SpringApplicationBuilder;
import org.springframework.boot.persistence.autoconfigure.EntityScan;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.context.ConfigurableApplicationContext;
import org.springframework.context.annotation.Import;
import static org.junit.jupiter.api.Assertions.assertNotNull;

/** Scan-free fixtures and passive witnesses only; no production view is removed or replaced. */
final class PersistentProbeSupport {
    private PersistentProbeSupport() {}

    static Class<?> application() throws ClassNotFoundException {
        return Class.forName(PersistentProbeSupport.class.getPackageName().replaceFirst("\\.runtime$", ".Application"));
    }

    static Path freshPath() throws Exception {
        var path = Path.of("target", "persistent-restart", UUID.randomUUID().toString(), "events").toAbsolutePath();
        Files.createDirectories(path.getParent());
        return path;
    }

    static String database(Path path) { return "jdbc:h2:file:" + path; }

    static ConfigurableApplicationContext viewless(String database) throws Exception {
        // Explicit sources, not component scanning: import the unchanged generated infrastructure.
        var infrastructure = Class.forName(application().getPackageName() + ".infrastructure.AxonConfiguration");
        var security = Class.forName(application().getPackageName() + ".infrastructure.SecurityConfiguration");
        var context = start(new SpringApplicationBuilder(Viewless.class, infrastructure, security), database);
        var unit = context.getBean(EntityManagerFactory.class).getMetamodel();
        assertNotNull(unit.entity(UniqueClaim.class));
        assertNotNull(unit.entity(AggregateEventEntry.class));
        assertNotNull(unit.entity(TokenEntry.class));
        System.out.println("Viewless fixture entities: " + unit.getEntities().stream()
                .map(entity -> entity.getJavaType().getName()).sorted().toList());
        return context;
    }

    static ConfigurableApplicationContext start(SpringApplicationBuilder builder, String database) {
        return builder.profiles("persistent").run("--server.port=0", "--spring.datasource.url=" + database,
                "--spring.jpa.hibernate.ddl-auto=update", "--spring.main.banner-mode=off");
    }

    @TestConfiguration(proxyBeanMethods = false)
    @EnableAutoConfiguration
    @EntityScan(basePackageClasses = { UniqueClaim.class,
            AggregateEventEntry.class,
            TokenEntry.class })
    @Import(PersistentConfiguration.class)
    static class Viewless {}
}
