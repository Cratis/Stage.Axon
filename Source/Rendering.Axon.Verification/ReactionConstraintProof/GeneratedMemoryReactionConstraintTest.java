package __APPLICATION__.runtime;

import io.cratis.stage.axon.runtime.InMemoryUniqueValueStore;
import org.springframework.boot.test.context.SpringBootTest;

@SpringBootTest
class GeneratedMemoryReactionConstraintTest extends GeneratedReactionConstraintTest {
    @Override
    protected Class<?> storeType() { return InMemoryUniqueValueStore.class; }
}
