package __RUNTIME__;

import __RUNTIME__.persistent.PersistentReadModelSafety;
import java.util.concurrent.atomic.AtomicBoolean;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.BeanDefinitionStoreException;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.context.SmartLifecycle;
import org.springframework.context.annotation.AnnotationConfigApplicationContext;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Profile;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

class PersistentReadModelSafetyTest {
    @Test
    void persistentViewsRefuseBeforeFactorySingletonAndLifecycleWork() {
        refuses("persistent");
    }

    @Test
    void mixedProfilesCannotBypassRefusal() {
        refuses("another", "persistent");
    }

    @Test
    void defaultViewsKeepTheirStoreAndRunNormally() {
        try (var context = context(GeneratedView.class)) {
            context.refresh();
            assertTrue(context.getBeansOfType(PersistentReadModelSafety.class).isEmpty());
            assertInstanceOf(InMemoryReadModelStore.class, context.getBean("test.View"));
            assertStarted(context.getBean(Witness.class));
        }
    }

    @Test
    void persistentViewlessContextsRunNormally() {
        try (var context = context(Viewless.class)) {
            context.getEnvironment().setActiveProfiles("persistent");
            context.refresh();
            assertTrue(context.getBeansOfType(PersistentReadModelSafety.class).isEmpty());
            assertTrue(context.getBeansOfType(ReadModelStore.class).isEmpty());
            assertStarted(context.getBean(Witness.class));
        }
    }

    private void refuses(String... profiles) {
        try (var context = context(GeneratedView.class)) {
            context.getEnvironment().setActiveProfiles(profiles);
            var witness = new Witness();
            context.registerBean(Witness.class, () -> witness);
            var failure = assertThrows(BeanDefinitionStoreException.class, context::refresh);
            assertTrue(failure.getMessage().contains("STAGE-AXON-0308"));
            assertTrue(failure.getMessage().contains("test.View"));
            assertTrue(failure.getMessage().contains("durable checkpoints may skip prior events"));
            assertFalse(witness.factory.get());
            assertFalse(witness.created.get());
            assertFalse(witness.running);
        }
    }

    private AnnotationConfigApplicationContext context(Class<?> configuration) {
        var context = new AnnotationConfigApplicationContext();
        context.registerBean(Witness.class, Witness::new);
        context.register(configuration);
        context.addBeanFactoryPostProcessor(factory -> factory.getBean(Witness.class).factory.set(true));
        context.registerBean("singletonWitness", Object.class, () -> {
            context.getBean(Witness.class).created.set(true);
            return new Object();
        });
        return context;
    }

    private void assertStarted(Witness witness) {
        assertTrue(witness.factory.get());
        assertTrue(witness.created.get());
        assertTrue(witness.running);
    }

    @TestConfiguration(proxyBeanMethods = false)
    static class Viewless {}

    @TestConfiguration(proxyBeanMethods = false)
    static class GeneratedView {
        @Bean("test.View")
        ReadModelStore<Object, Object> store() { return new InMemoryReadModelStore<>(); }

        @Bean("test.View.persistentReadModelSafety")
        @Profile("persistent")
        static PersistentReadModelSafety persistentReadModelSafety() {
            return new PersistentReadModelSafety("test.View");
        }
    }

    static class Witness implements SmartLifecycle {
        final AtomicBoolean factory = new AtomicBoolean();
        final AtomicBoolean created = new AtomicBoolean();
        boolean running;
        public void start() { running = true; }
        public void stop() { running = false; }
        public boolean isRunning() { return running; }
    }
}
