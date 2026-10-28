package io.cratis.stage.axon.starter;

import java.util.Collection;
import java.util.HashMap;
import java.util.Map;
import org.axonframework.common.infra.ComponentDescriptor;

final class RecordingComponentDescriptor implements ComponentDescriptor {
    private final Map<String, Object> properties = new HashMap<>();

    Object property(String name) { return properties.get(name); }
    boolean hasProperty(String name) { return properties.containsKey(name); }

    @Override public void describeProperty(String name, Object value) { properties.put(name, value); }
    @Override public void describeProperty(String name, Collection<?> value) { properties.put(name, value); }
    @Override public void describeProperty(String name, Map<?, ?> value) { properties.put(name, value); }
    @Override public void describeProperty(String name, String value) { properties.put(name, value); }
    @Override public void describeProperty(String name, Long value) { properties.put(name, value); }
    @Override public void describeProperty(String name, Boolean value) { properties.put(name, value); }
}
