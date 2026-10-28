package io.cratis.stage.axon.scan;

import io.cratis.stage.axon.runtime.persistent.UniqueClaim;
import io.cratis.stage.axon.runtime.views.ViewRow;
import io.cratis.stage.axon.runtime.views.ViewGeneration;
import jakarta.persistence.EntityManagerFactory;
import org.axonframework.eventsourcing.eventstore.jpa.AggregateEventEntry;
import org.axonframework.messaging.eventhandling.processing.streaming.token.store.jpa.TokenEntry;
import org.junit.jupiter.api.Test;
import org.springframework.boot.builder.SpringApplicationBuilder;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

class ExplicitEntityScanTest {
    @Test
    void keepsAppEntitiesAndTheClaimWithAnExplicitList() {
        try (var context = new SpringApplicationBuilder(ScanFixtureApplication.class, ExplicitScanConfiguration.class)
                .profiles("persistent").properties("spring.datasource.url=jdbc:h2:mem:s4scanexplicit;DB_CLOSE_DELAY=-1").run()) {
            var factory = context.getBean(EntityManagerFactory.class);
            assertTrue(hasEntity(factory, FixtureEntity.class));
            assertTrue(hasEntity(factory, UniqueClaim.class));
            assertTrue(hasEntity(factory, AggregateEventEntry.class));
            assertTrue(hasEntity(factory, TokenEntry.class));
            assertTrue(hasEntity(factory, ViewRow.class));
            assertTrue(hasEntity(factory, ViewGeneration.class));
            try (var manager = factory.createEntityManager()) {
                assertEquals(0L, ((Number) manager.createNativeQuery("select count(*) from stage_unique_claim").getSingleResult()).longValue());
                assertEquals(0L, ((Number) manager.createNativeQuery("select count(*) from fixture_entity").getSingleResult()).longValue());
                assertEquals(0L, ((Number) manager.createNativeQuery("select count(*) from stage_view_row").getSingleResult()).longValue());
                assertEquals(0L, ((Number) manager.createNativeQuery("select count(*) from stage_view_generation").getSingleResult()).longValue());
            }
        }
    }

    /** An explicit entity scan replaces default packages, so every generated list must include UniqueClaim. */
    @Test
    void explicitListWithoutTheClaimDropsIt() {
        try (var context = new SpringApplicationBuilder(ScanFixtureApplication.class, ScanWithoutClaimConfiguration.class)
                .profiles("persistent").properties("spring.datasource.url=jdbc:h2:mem:s4scanwithoutclaim;DB_CLOSE_DELAY=-1").run()) {
            var factory = context.getBean(EntityManagerFactory.class);
            assertFalse(hasEntity(factory, UniqueClaim.class));
            assertTrue(hasEntity(factory, FixtureEntity.class));
        }
    }

    @Test
    void defaultProfileRegistersBothWithoutAnyScan() {
        try (var context = new SpringApplicationBuilder(ScanFixtureApplication.class, ExplicitScanConfiguration.class)
                .properties("spring.datasource.url=jdbc:h2:mem:s4scandefault;DB_CLOSE_DELAY=-1").run()) {
            var factory = context.getBean(EntityManagerFactory.class);
            assertTrue(hasEntity(factory, FixtureEntity.class));
            assertTrue(hasEntity(factory, UniqueClaim.class));
        }
    }

    private static boolean hasEntity(EntityManagerFactory factory, Class<?> type) {
        return factory.getMetamodel().getEntities().stream().anyMatch(entity -> entity.getJavaType() == type);
    }
}
