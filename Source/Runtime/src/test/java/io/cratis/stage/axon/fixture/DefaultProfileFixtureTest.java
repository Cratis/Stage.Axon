package io.cratis.stage.axon.fixture;

import io.cratis.stage.axon.runtime.InMemoryUniqueValueStore;
import io.cratis.stage.axon.runtime.UniqueValueStore;
import io.cratis.stage.axon.runtime.persistent.UniqueClaim;
import io.cratis.stage.axon.runtime.views.ViewRow;
import jakarta.persistence.EntityManagerFactory;
import io.cratis.stage.axon.runtime.persistent.PersistentConfiguration;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.context.ApplicationContext;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertTrue;

@SpringBootTest(classes = FixtureApplication.class, properties = "spring.datasource.url=jdbc:h2:mem:s3default;DB_CLOSE_DELAY=-1")
class DefaultProfileFixtureTest {
    @Autowired ApplicationContext context;
    @Autowired EntityManagerFactory factory;

    @Test
    void keepsTheInMemoryUniqueValueStore() {
        var stores = context.getBeansOfType(UniqueValueStore.class);
        assertEquals(1, stores.size());
        assertInstanceOf(InMemoryUniqueValueStore.class, stores.values().iterator().next());
    }

    @Test
    void hasNoEventStorageEngineEnhancer() {
        assertFalse(context.containsBean("eventStorageEngine"));
        assertTrue(context.getBeansOfType(PersistentConfiguration.class).isEmpty());
    }

    @Test
    void doesNotRegisterViewRowsWithoutAScan() {
        assertFalse(factory.getMetamodel().getEntities().stream().anyMatch(entity -> entity.getJavaType() == ViewRow.class));
    }

    @Test
    void stillRegistersTheClaimEntity() {
        assertTrue(factory.getMetamodel().getEntities().stream().anyMatch(entity -> entity.getJavaType() == UniqueClaim.class));
    }
}
