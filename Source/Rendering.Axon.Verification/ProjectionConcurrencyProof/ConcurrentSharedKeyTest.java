package __APPLICATION__.pricing.basket.totals;

import __APPLICATION__.concepts.Money;
import __APPLICATION__.concepts.Quantity;
import __APPLICATION__.pricing.events.LineAdded;
import io.cratis.stage.axon.runtime.EventSourceId;
import java.math.BigDecimal;
import java.util.ArrayList;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import org.axonframework.messaging.eventhandling.gateway.EventGateway;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import static org.assertj.core.api.Assertions.assertThat;

@SpringBootTest
class ConcurrentSharedKeyTest {
    static final int Threads = 8;
    static final int PerThread = 25;
    @Autowired EventGateway events;
    @Autowired BasketTotalRepository repository;

    @Test
    void sumsEveryConcurrentEventOnTheSharedKey() throws Exception {
        var pool = Executors.newFixedThreadPool(Threads);
        try {
            var start = new CountDownLatch(1);
            var futures = new ArrayList<CompletableFuture<Void>>();
            for (var thread = 0; thread < Threads; thread++) {
                futures.add(CompletableFuture.runAsync(() -> {
                    try {
                        start.await();
                        for (var index = 0; index < PerThread; index++) {
                            events.publish(List.of(new LineAdded(new EventSourceId(UUID.randomUUID().toString()), new Money(new BigDecimal("1.5")), new Quantity(2L)))).get(30, TimeUnit.SECONDS);
                        }
                    } catch (Exception exception) {
                        throw new IllegalStateException(exception);
                    }
                }, pool));
            }
            start.countDown();
            CompletableFuture.allOf(futures.toArray(CompletableFuture[]::new)).get(60, TimeUnit.SECONDS);
        } finally {
            pool.shutdownNow();
        }
        var expected = new BigDecimal("3.0").multiply(BigDecimal.valueOf((long) Threads * PerThread));
        var deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(30);
        BigDecimal actual = null;
        while (System.nanoTime() < deadline) {
            actual = repository.findById("global").map(row -> row.toModel().total().value()).orElse(null);
            if (actual != null && actual.compareTo(expected) == 0) break;
            Thread.sleep(50);
        }
        assertThat(actual).isNotNull();
        assertThat(actual).isEqualByComparingTo(expected);
    }
}
