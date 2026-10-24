// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

package __RUNTIME__.json;

import java.lang.reflect.Constructor;
import java.lang.reflect.InvocationTargetException;
import tools.jackson.databind.DeserializationContext;

final class ConceptConstructor {
    private final Class<?> type;
    private final Class<?> primitive;
    private final Constructor<?> constructor;

    ConceptConstructor(Class<?> type) {
        this.type = type;
        var components = type.getRecordComponents();
        if (components.length != 1 || !components[0].getName().equals("value")) throw new IllegalArgumentException("Concept records must declare exactly one value component: " + type.getName());
        primitive = components[0].getType();
        try { constructor = type.getConstructor(primitive); }
        catch (NoSuchMethodException exception) { throw new IllegalArgumentException("Concept records need a public canonical constructor", exception); }
    }

    Class<?> primitive() { return primitive; }

    Object create(Object value, DeserializationContext context) {
        try { return constructor.newInstance(value); }
        catch (InvocationTargetException exception) { throw context.instantiationException(type, exception.getCause()); }
        catch (ReflectiveOperationException exception) { throw context.instantiationException(type, exception); }
    }
}
