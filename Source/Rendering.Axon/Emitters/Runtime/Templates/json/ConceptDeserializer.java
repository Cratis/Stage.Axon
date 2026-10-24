// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

package __RUNTIME__.json;

import tools.jackson.core.JsonParser;
import tools.jackson.databind.DeserializationContext;
import tools.jackson.databind.ValueDeserializer;

final class ConceptDeserializer extends ValueDeserializer<Object> {
    private final ConceptConstructor constructor;
    ConceptDeserializer(Class<?> type) { constructor = new ConceptConstructor(type); }

    @Override
    public Object deserialize(JsonParser parser, DeserializationContext context) {
        return constructor.create(context.readValue(parser, constructor.primitive()), context);
    }
}
