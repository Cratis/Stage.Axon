package io.cratis.stage.axon.runtime.views;

import java.util.HashSet;
import java.util.Set;
import org.axonframework.common.configuration.Configuration;
import org.axonframework.messaging.eventhandling.processing.EventProcessor;
import org.axonframework.messaging.eventhandling.processing.errorhandling.PropagatingErrorHandler;
import org.axonframework.messaging.eventhandling.processing.streaming.StreamingEventProcessor;
import org.axonframework.messaging.eventhandling.processing.streaming.pooled.PooledStreamingEventProcessor;
import org.axonframework.messaging.eventhandling.processing.streaming.pooled.PooledStreamingEventProcessorConfiguration;

/** Check resolved configurations after all customizations, before Spring starts processor lifecycles. */
public final class PersistentViewProcessorGuard {
    private PersistentViewProcessorGuard() {}
    public static void verify(Configuration configuration, Set<String> required) {
        var verified = new HashSet<String>();
        inspect(configuration, required, verified);
        if (!verified.containsAll(required)) throw refusal();
        var processors = configuration.getComponents(StreamingEventProcessor.class);
        var otherProcessors = configuration.getComponents(EventProcessor.class);
        for (var name : required) {
            if (!(processors.get(name) instanceof PooledStreamingEventProcessor)) throw refusal();
            if (otherProcessors.containsKey(name) && !(otherProcessors.get(name) instanceof PooledStreamingEventProcessor)) throw refusal();
        }
    }
    private static void inspect(Configuration configuration, Set<String> required, Set<String> verified) {
        configuration.getOptionalComponent(PooledStreamingEventProcessorConfiguration.class).ifPresent(processor -> {
            if (required.contains(processor.processorName())) {
                if (processor.errorHandler() != PropagatingErrorHandler.INSTANCE) throw refusal();
                verified.add(processor.processorName());
            }
        });
        configuration.getModuleConfigurations().forEach(module -> inspect(module, required, verified));
    }
    private static IllegalStateException refusal() {
        return new IllegalStateException("STAGE-AXON-0308: Persistent flat views require pooled processors with PropagatingErrorHandler after all customizations");
    }
}
