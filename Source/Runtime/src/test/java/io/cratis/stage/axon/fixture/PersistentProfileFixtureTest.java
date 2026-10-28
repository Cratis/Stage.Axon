package io.cratis.stage.axon.fixture;

import io.cratis.stage.axon.runtime.UniqueValueStore;
import io.cratis.stage.axon.runtime.persistent.JpaUniqueValueStore;
import io.cratis.stage.axon.runtime.persistent.UniqueClaim;
import io.cratis.stage.axon.runtime.views.ViewRow;
import jakarta.persistence.EntityManagerFactory;
import org.axonframework.common.configuration.ConfigurationEnhancer;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.context.ApplicationContext;
import org.springframework.test.context.ActiveProfiles;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertTrue;

@SpringBootTest(classes = FixtureApplication.class, properties = "spring.datasource.url=jdbc:h2:mem:s3persistent;DB_CLOSE_DELAY=-1")
@ActiveProfiles("persistent")
class PersistentProfileFixtureTest {
    @Autowired ApplicationContext context;
    @Autowired EntityManagerFactory factory;

    @Test
    void providesExactlyTheJpaUniqueValueStore() {
        var stores = context.getBeansOfType(UniqueValueStore.class);
        assertEquals(1, stores.size());
        assertInstanceOf(JpaUniqueValueStore.class, stores.values().iterator().next());
    }

    @Test
    void providesTheEventStorageEngineEnhancer() {
        assertInstanceOf(ConfigurationEnhancer.class, context.getBean("eventStorageEngine"));
    }

    @Test
    void doesNotRegisterViewRowsWithoutAScan() {
        assertFalse(factory.getMetamodel().getEntities().stream().anyMatch(entity -> entity.getJavaType() == ViewRow.class));
    }

    @Test
    void registersTheClaimEntityWithoutAnEntityScan() {
        assertTrue(factory.getMetamodel().getEntities().stream().anyMatch(entity -> entity.getJavaType() == UniqueClaim.class));
        try (var manager = factory.createEntityManager()) {
            assertEquals(0L, ((Number) manager.createNativeQuery("select count(*) from stage_unique_claim").getSingleResult()).longValue());
        }
    }
}
