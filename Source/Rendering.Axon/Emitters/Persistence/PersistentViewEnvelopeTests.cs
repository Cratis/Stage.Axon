// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence;

internal static class PersistentViewEnvelopeTests
{
    internal const string Source = """
        package __PACKAGE__;

        import org.axonframework.extension.spring.config.EventProcessorDefinition;
        import org.axonframework.messaging.eventhandling.processing.streaming.pooled.PooledStreamingEventProcessorModule;
        import org.junit.jupiter.api.Test;
        import org.springframework.beans.factory.config.BeanPostProcessor;
        import org.springframework.boot.test.context.TestConfiguration;
        import org.springframework.context.annotation.Bean;
        import static org.junit.jupiter.api.Assertions.*;
        import static __PACKAGE__.__NAME__Support.*;

        public class __NAME__EnvelopeTest {
            @Test void refusesSwallowingErrorCustomizationBeforeReadiness() {
                assertRefused(SwallowErrors.class);
            }
            @Test void refusesSubscribingReplacementBeforeReadiness() {
                assertRefused(Subscribing.class);
            }
            private void assertRefused(Class<?> customization) {
                var error = assertThrows(Exception.class, () -> {
                    try (var application = start(database(), customization)) { fail("Unsafe processor reached readiness: active=" + application.isActive()); }
                });
                var messages = new StringBuilder();
                for (Throwable cause = error; cause != null; cause = cause.getCause()) messages.append(cause.getMessage());
                assertTrue(messages.toString().contains("Persistent flat views require pooled processors with PropagatingErrorHandler"));
            }
            @TestConfiguration(proxyBeanMethods = false)
            static class SwallowErrors {
                @Bean PooledStreamingEventProcessorModule.Customization swallowErrors() {
                    return (configuration, processor) -> {
                        processor.errorHandler(error -> {});
                        return processor;
                    };
                }
            }
            @TestConfiguration(proxyBeanMethods = false)
            static class Subscribing {
                @Bean static BeanPostProcessor replacePersistentProcessor() {
                    return new BeanPostProcessor() {
                        @Override public Object postProcessAfterInitialization(Object bean, String name) {
                            return bean instanceof EventProcessorDefinition && name.startsWith("persistentViewProcessor")
                                    ? EventProcessorDefinition.subscribingMatching("__PACKAGE__").notCustomized() : bean;
                        }
                    };
                }
            }
        }
        """;
}
