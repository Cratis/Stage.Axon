// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

package __RUNTIME__.json;

import tools.jackson.core.JsonGenerator;
import tools.jackson.databind.SerializationContext;
import tools.jackson.databind.ValueSerializer;
import __RUNTIME__.ConceptAs;

final class ConceptSerializer extends ValueSerializer<ConceptAs<?>> {
    private final boolean key;
    ConceptSerializer(boolean key) { this.key = key; }

    @Override
    public void serialize(ConceptAs<?> concept, JsonGenerator generator, SerializationContext context) {
        if (key) generator.writeName(String.valueOf(concept.value()));
        else context.writeValue(generator, concept.value());
    }
}
