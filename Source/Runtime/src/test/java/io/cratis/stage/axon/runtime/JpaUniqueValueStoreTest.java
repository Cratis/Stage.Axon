package io.cratis.stage.axon.runtime;

import jakarta.persistence.EntityManagerFactory;
import java.util.concurrent.CompletableFuture;
import java.util.function.Consumer;
import org.axonframework.messaging.core.unitofwork.ProcessingContext;
import org.axonframework.messaging.core.unitofwork.transaction.jpa.EntityManagerTransactionManager;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.test.context.ActiveProfiles;

@SpringBootTest(classes = RuntimeTestApplication.class, properties = {
        "spring.datasource.url=jdbc:h2:mem:occurrence;DB_CLOSE_DELAY=-1;LOCK_TIMEOUT=5000",
        "spring.jpa.hibernate.ddl-auto=create-drop"})
@ActiveProfiles("persistent")
class JpaUniqueValueStoreTest extends UniqueValueStoreContractTest {
    @Autowired private UniqueValueStore values;
    @Autowired private EntityManagerFactory entities;

    @Override protected UniqueValueStore store() { return values; }

    @Override protected void run(Consumer<ProcessingContext> action) {
        try (var manager = entities.createEntityManager()) {
            var transactions = new EntityManagerTransactionManager(() -> manager);
            var work = factory().create("occurrence", config -> config.forcedSameThreadInvocation());
            transactions.attachToProcessingLifecycle(work);
            work.executeWithResult(context -> {
                action.accept(context);
                return CompletableFuture.completedFuture(null);
            }).join();
        }
    }
}
