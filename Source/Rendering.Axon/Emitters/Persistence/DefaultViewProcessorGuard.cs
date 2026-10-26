// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence;

internal static class DefaultViewProcessorGuard
{
    internal const string Source = """
        // Copyright (c) Cratis. All rights reserved.
        // Licensed under the MIT license. See LICENSE file in the project root for full license information.
        package __PACKAGE__;

        import java.util.Set;
        import org.axonframework.common.configuration.Configuration;
        import org.axonframework.messaging.eventhandling.processing.EventProcessor;
        import org.axonframework.messaging.eventhandling.processing.streaming.StreamingEventProcessor;
        import org.springframework.beans.factory.SmartInitializingSingleton;
        import org.springframework.context.annotation.Bean;
        import org.springframework.context.annotation.Profile;

        /** Volatile views must not run behind streaming checkpoints in the default profile. */
        @org.springframework.context.annotation.Configuration(proxyBeanMethods = false)
        @Profile("!persistent")
        public class DefaultViewProcessorGuard {
            @Bean
            SmartInitializingSingleton defaultViewProcessorAdmission(Configuration configuration) {
                return () -> verify(configuration, Set.of(__NAMESPACES__));
            }
            public static void verify(Configuration configuration, Set<String> required) {
                var processors = configuration.getComponents(EventProcessor.class);
                var streaming = configuration.getComponents(StreamingEventProcessor.class);
                for (var name : required) {
                    var processor = processors.get(name);
                    if (streaming.containsKey(name)) throw refusal(name);
                    if (processor == null) throw refusal(name);
                    if (processor instanceof StreamingEventProcessor) throw refusal(name);
                }
            }
            private static IllegalStateException refusal(String name) {
                return new IllegalStateException("Default-profile view processor '" + name +
                        "' must exist and must not be streaming; configure subscribing processing for volatile views");
            }
        }
        """;
}
