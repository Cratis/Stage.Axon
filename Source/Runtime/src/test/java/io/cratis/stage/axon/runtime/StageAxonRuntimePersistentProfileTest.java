package io.cratis.stage.axon.runtime;

import io.cratis.stage.axon.runtime.persistent.JpaUniqueValueStore;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.context.ApplicationContext;
import org.springframework.test.context.ActiveProfiles;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;

@SpringBootTest(classes = RuntimeTestApplication.class)
@ActiveProfiles("persistent")
class StageAxonRuntimePersistentProfileTest {
    @Autowired ApplicationContext context;

    @Test
    void leavesTheUniqueValueStoreToThePersistentConfiguration() {
        assertEquals(1, context.getBeansOfType(UniqueValueStore.class).size());
        assertInstanceOf(JpaUniqueValueStore.class, context.getBean(UniqueValueStore.class));
        assertEquals(1, context.getBeansOfType(LiveStreams.class).size());
    }
}
