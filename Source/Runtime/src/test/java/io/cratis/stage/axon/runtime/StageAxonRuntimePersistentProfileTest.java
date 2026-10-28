package io.cratis.stage.axon.runtime;

import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.context.ApplicationContext;
import org.springframework.test.context.ActiveProfiles;
import static org.junit.jupiter.api.Assertions.assertEquals;

@SpringBootTest(classes = RuntimeTestApplication.class)
@ActiveProfiles("persistent")
class StageAxonRuntimePersistentProfileTest {
    @Autowired ApplicationContext context;

    @Test
    void leavesTheUniqueValueStoreToThePersistentConfiguration() {
        assertEquals(0, context.getBeansOfType(UniqueValueStore.class).size());
        assertEquals(1, context.getBeansOfType(LiveStreams.class).size());
    }
}
