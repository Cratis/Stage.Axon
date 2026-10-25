// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

package __RUNTIME__;

import jakarta.persistence.EntityManagerFactory;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;
import java.util.UUID;
import javax.sql.DataSource;
import org.axonframework.eventsourcing.eventstore.EventStorageEngine;
import org.axonframework.messaging.eventhandling.processing.streaming.token.store.TokenStore;
import org.springframework.beans.factory.config.BeanPostProcessor;
import org.springframework.boot.autoconfigure.EnableAutoConfiguration;
import org.springframework.boot.builder.SpringApplicationBuilder;
import org.springframework.boot.context.event.ApplicationReadyEvent;
import org.springframework.boot.persistence.autoconfigure.EntityScan;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.boot.web.server.context.WebServerInitializedEvent;
import org.springframework.context.ApplicationEvent;
import org.springframework.context.ApplicationListener;
import org.springframework.context.ConfigurableApplicationContext;
import org.springframework.context.SmartLifecycle;
import org.springframework.context.annotation.Import;
import __RUNTIME__.persistent.PersistentConfiguration;
import __RUNTIME__.persistent.PersistentReadModelSafety;
import static org.junit.jupiter.api.Assertions.*;

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
        assertTrue(context.getBeansOfType(ReadModelStore.class).isEmpty(), "Fixture must be viewless");
        assertTrue(context.getBeansOfType(PersistentReadModelSafety.class).isEmpty(), "Fixture must have no guard");
        var unit = context.getBean(EntityManagerFactory.class).getMetamodel();
        assertNotNull(unit.entity(__RUNTIME__.persistent.UniqueClaim.class));
        assertNotNull(unit.entity(org.axonframework.eventsourcing.eventstore.jpa.AggregateEventEntry.class));
        assertNotNull(unit.entity(org.axonframework.messaging.eventhandling.processing.streaming.token.store.jpa.TokenEntry.class));
        System.out.println("Viewless fixture entities: " + unit.getEntities().stream()
                .map(entity -> entity.getJavaType().getName()).sorted().toList());
        return context;
    }

    static ConfigurableApplicationContext start(SpringApplicationBuilder builder, String database) {
        return builder.profiles("persistent").run("--server.port=0", "--spring.datasource.url=" + database,
                "--spring.jpa.hibernate.ddl-auto=update", "--spring.main.banner-mode=off");
    }

    static void refuses(String database) throws Exception {
        var witness = new Witness();
        var builder = new SpringApplicationBuilder(application())
                .listeners((ApplicationListener<ApplicationEvent>) event -> {
                    if (event instanceof ApplicationReadyEvent || event instanceof WebServerInitializedEvent) witness.ready = true;
                })
                .initializers(context -> {
                    context.getBeanFactory().addBeanPostProcessor(witness);
                    context.addBeanFactoryPostProcessor(factory -> {
                        witness.factory = true;
                        // Deliberately dangerous late factory work must never execute.
                        factory.getBean(DataSource.class);
                        factory.getBean(EntityManagerFactory.class);
                    });
                });
        var failure = assertThrows(Exception.class, () -> start(builder, database));
        var root = failure;
        while (root.getCause() != null) root = (Exception) root.getCause();
        assertInstanceOf(org.springframework.beans.factory.BeanDefinitionStoreException.class, root);
        assertTrue(root.getMessage().contains("STAGE-AXON-0308"), root.getMessage());
        assertTrue(root.getMessage().contains(application().getPackageName() + "."), root.getMessage());
        assertFalse(witness.ready, "No HTTP initialization or application readiness");
        assertFalse(witness.factory, "Registry refusal must precede ordinary factory processing");
        assertTrue(witness.resources.isEmpty(), "No resource or lifecycle creation: " + witness.resources);
        System.out.println("Persistent-view refusal: 0308; no readiness, factory, resources or lifecycle creation");
    }

    @TestConfiguration(proxyBeanMethods = false)
    @EnableAutoConfiguration
    @EntityScan(basePackageClasses = { __RUNTIME__.persistent.UniqueClaim.class,
            org.axonframework.eventsourcing.eventstore.jpa.AggregateEventEntry.class,
            org.axonframework.messaging.eventhandling.processing.streaming.token.store.jpa.TokenEntry.class })
    @Import(PersistentConfiguration.class)
    static class Viewless {}

    static class Witness implements BeanPostProcessor {
        private static final List<Class<?>> WATCHED = List.of(DataSource.class, EntityManagerFactory.class,
                TokenStore.class, EventStorageEngine.class, ReadModelStore.class, SmartLifecycle.class);
        final ArrayList<String> resources = new ArrayList<>();
        boolean ready;
        boolean factory;

        @Override
        public Object postProcessBeforeInitialization(Object bean, String name) {
            if (WATCHED.stream().anyMatch(type -> type.isInstance(bean))) {
                resources.add(name);
            }
            return bean;
        }
    }
}
