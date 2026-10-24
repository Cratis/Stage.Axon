// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

package __RUNTIME__.json;

import java.io.Serial;
import com.fasterxml.jackson.annotation.JsonFormat;
import tools.jackson.databind.BeanDescription;
import tools.jackson.databind.DeserializationConfig;
import tools.jackson.databind.JavaType;
import tools.jackson.databind.SerializationConfig;
import tools.jackson.databind.ValueDeserializer;
import tools.jackson.databind.ValueSerializer;
import tools.jackson.databind.JacksonModule.SetupContext;
import tools.jackson.databind.deser.Deserializers;
import tools.jackson.databind.module.SimpleModule;
import tools.jackson.databind.ser.Serializers;
import __RUNTIME__.ConceptAs;

public final class ConceptsModule extends SimpleModule {
    @Serial
    private static final long serialVersionUID = 1L;

    public ConceptsModule() { super("ConceptsModule"); }

    @Override
    public void setupModule(SetupContext context) {
        super.setupModule(context);
        context.addSerializers(serializers(false));
        context.addKeySerializers(serializers(true));
        context.addDeserializers(new Deserializers.Base() {
            @Override
            public boolean hasDeserializerFor(DeserializationConfig config, Class<?> type) { return isConcept(type); }

            @Override
            public ValueDeserializer<?> findBeanDeserializer(JavaType type, DeserializationConfig config, BeanDescription.Supplier bean) {
                return isConcept(type.getRawClass()) ? new ConceptDeserializer(type.getRawClass()) : null;
            }
        });
        context.addKeyDeserializers((type, config, bean) -> isConcept(type.getRawClass()) ? new ConceptKeyDeserializer(type.getRawClass()) : null);
    }

    private static boolean isConcept(Class<?> type) { return type.isRecord() && ConceptAs.class.isAssignableFrom(type); }

    private static Serializers serializers(boolean key) {
        return new Serializers.Base() {
            @Override
            public ValueSerializer<?> findSerializer(SerializationConfig config, JavaType type, BeanDescription.Supplier bean, JsonFormat.Value format) {
                return isConcept(type.getRawClass()) ? new ConceptSerializer(key) : null;
            }
        };
    }
}
