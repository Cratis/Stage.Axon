// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

package __RUNTIME__.json;

import tools.jackson.databind.DeserializationContext;
import tools.jackson.databind.KeyDeserializer;

final class ConceptKeyDeserializer extends KeyDeserializer {
    private final ConceptConstructor constructor;
    ConceptKeyDeserializer(Class<?> type) { constructor = new ConceptConstructor(type); }

    @Override
    public Object deserializeKey(String key, DeserializationContext context) {
        var primitive = context.findKeyDeserializer(context.constructType(constructor.primitive()), null).deserializeKey(key, context);
        return constructor.create(primitive, context);
    }
}
