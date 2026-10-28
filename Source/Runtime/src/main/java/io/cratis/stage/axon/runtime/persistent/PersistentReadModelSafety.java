package io.cratis.stage.axon.runtime.persistent;

import org.springframework.beans.factory.BeanDefinitionStoreException;
import org.springframework.beans.factory.config.ConfigurableListableBeanFactory;
import org.springframework.beans.factory.support.BeanDefinitionRegistry;
import org.springframework.beans.factory.support.BeanDefinitionRegistryPostProcessor;
import org.springframework.core.PriorityOrdered;

/** Refuses persistent views before ordinary factory processing, resources or processors start. */
public final class PersistentReadModelSafety implements BeanDefinitionRegistryPostProcessor, PriorityOrdered {
    private final String model;

    /** @param model the fully qualified generated view whose state cannot be recovered safely */
    public PersistentReadModelSafety(String model) {
        this.model = model;
    }

    @Override
    public int getOrder() {
        return HIGHEST_PRECEDENCE;
    }

    @Override
    public void postProcessBeanDefinitionRegistry(BeanDefinitionRegistry registry) {
        throw new BeanDefinitionStoreException("STAGE-AXON-0308: Persistent startup with generated view " + model
                + " is refused. Read-model rows and projector indexes are volatile; durable checkpoints may skip prior events"
                + " after restart. Automatic replay is not established recovery.");
    }

    @Override
    public void postProcessBeanFactory(ConfigurableListableBeanFactory factory) {
        // No factory work: refusal happens earlier, during registry processing.
    }
}
