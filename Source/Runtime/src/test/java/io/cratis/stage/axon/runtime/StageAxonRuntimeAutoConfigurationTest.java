package io.cratis.stage.axon.runtime;

import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.context.ApplicationContext;
import static org.junit.jupiter.api.Assertions.assertDoesNotThrow;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;

@SpringBootTest(classes = RuntimeTestApplication.class, properties = "stage.axon.live.max-streams=1")
class StageAxonRuntimeAutoConfigurationTest {
    @Autowired ApplicationContext context;

    @Test
    void theOrderingTargetNamedByTheAutoConfigurationExists() {
        assertDoesNotThrow(() -> Class.forName("org.springframework.boot.webmvc.autoconfigure.WebMvcAutoConfiguration"));
    }

    @Test
    void registersTheHelpersOnceWithTheDefaultProfile() {
        assertEquals(1, context.getBeansOfType(LiveStreams.class).size());
        assertEquals(1, context.getBeansOfType(ProblemDetailsAdvice.class).size());
        var stores = context.getBeansOfType(UniqueValueStore.class);
        assertEquals(1, stores.size());
        assertInstanceOf(InMemoryUniqueValueStore.class, stores.values().iterator().next());
    }
}
