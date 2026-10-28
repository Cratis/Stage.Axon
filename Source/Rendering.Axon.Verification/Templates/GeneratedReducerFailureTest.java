package __APPLICATION__.runtime;

import __APPLICATION__.accounts.totals.record_.RecordCount;
import __APPLICATION__.accounts.totals.delete.DeleteCounter;
import __APPLICATION__.accounts.totals.quarantined.QuarantinedTotal;
import __APPLICATION__.accounts.totals.quarantined.QuarantinedTotals;
import __APPLICATION__.accounts.totals.quarantined.QuarantinedTotalsHandler;
import __APPLICATION__.accounts.totals.quarantined.LiveQuarantinedTotals;
import __APPLICATION__.accounts.totals.quarantined.LiveQuarantinedTotalsSubscription;
import __APPLICATION__.accounts.totals.quarantined.OpaqueQuarantinedTotals;
import __APPLICATION__.accounts.totals.quarantined.OpaqueQuarantinedTotalsHandler;
import __APPLICATION__.accounts.totals.healthy.HealthyTotal;
import __APPLICATION__.accounts.totals.healthy.HealthyTotals;
import __APPLICATION__.accounts.totals.healthy.UnrelatedOpaqueTotals;
import __APPLICATION__.accounts.totals.healthy.UnrelatedOpaqueTotalsHandler;
import __APPLICATION__.accounts.totals.healthy.UnrelatedOpaqueTotalsPerformer;
import __APPLICATION__.concepts.CounterId;
import __APPLICATION__.persistence.JpaReadModelStore;
import io.cratis.stage.axon.runtime.ReadModelStore;
import jakarta.persistence.EntityManagerFactory;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.time.Duration;
import java.util.HexFormat;
import java.util.Set;
import java.util.UUID;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.Flow;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.atomic.AtomicReference;
import org.axonframework.messaging.commandhandling.gateway.CommandGateway;
import org.axonframework.messaging.queryhandling.gateway.QueryGateway;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.Timeout;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.context.ApplicationContext;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.web.servlet.MockMvc;
import tools.jackson.databind.json.JsonMapper;
import static org.awaitility.Awaitility.await;
import static org.junit.jupiter.api.Assertions.*;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.*;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

@SpringBootTest(properties = {
        "spring.datasource.url=jdbc:h2:mem:reducer-quarantine;DB_CLOSE_DELAY=-1;LOCK_TIMEOUT=5000",
        "spring.jpa.hibernate.ddl-auto=create-drop"})
@ActiveProfiles("__PROFILE__")
@AutoConfigureMockMvc
@Timeout(30)
class GeneratedReducerFailureTest {
    private static final String NAMESPACE = "__APPLICATION__.accounts.totals.quarantined.QuarantinedTotal";
    @Autowired ApplicationContext application;
    @Autowired EntityManagerFactory factory;
    @Autowired JsonMapper mapper;
    @Autowired MockMvc http;
    @Autowired CommandGateway commands;
    @Autowired QueryGateway queries;
    @Autowired QuarantinedTotalsHandler handler;
    @Autowired OpaqueQuarantinedTotalsHandler opaque;
    @Autowired UnrelatedOpaqueTotalsHandler unrelated;
    @Autowired UnrelatedOpaqueTotalsPerformer unrelatedPerformer;
    @Autowired @Qualifier(NAMESPACE) ReadModelStore<Object, QuarantinedTotal> store;
    @Autowired @Qualifier("__APPLICATION__.accounts.totals.healthy.HealthyTotal") ReadModelStore<Object, HealthyTotal> healthy;

    @Test
    void retainedRowsCannotLeakOrBeMutatedWhileTheWriterIsAbsent() throws Exception {
        var retained = retainRow();
        assertEquals(991L, retained.get("retained").orElseThrow().amount());
        assertThrows(ClassNotFoundException.class, () -> Class.forName(NAMESPACE + "ReducerProjector"));
        assertThrows(IllegalStateException.class, () -> store.get("retained"));
        assertThrows(IllegalStateException.class, store::all);
        assertThrows(IllegalStateException.class, () -> store.put("retained", null));
        assertThrows(IllegalStateException.class, () -> store.remove("retained"));
        assertThrows(IllegalStateException.class, store::clear);
        assertThrows(IllegalStateException.class, () -> store.update("retained", current -> { fail("No callback may see a row"); return current; }));
        assertThrows(IllegalStateException.class, () -> handler.handle(new QuarantinedTotals()));
        assertThrows(IllegalStateException.class, () -> opaque.handle(new OpaqueQuarantinedTotals()));
        assertThrows(Exception.class, () -> queries.query(new QuarantinedTotals(), QuarantinedTotals.Result.class).get(10, TimeUnit.SECONDS));
        assertUnavailableSubscription();
        commands.sendAndWait(new RecordCount(new CounterId("retained"), 1L));
        commands.sendAndWait(new DeleteCounter(new CounterId("retained")));
        await().atMost(Duration.ofSeconds(10)).untilAsserted(() -> assertTrue(healthy.all().stream().anyMatch(row -> row.id().value().equals("retained"))));
        assertEquals(991L, retained.get("retained").orElseThrow().amount(), "Retained rows are neither adopted nor reset");
    }

    @Test
    void anUnaffectedModeledQueryWorksButAnUnrelatedOpaquePerformerIsRefused() throws Exception {
        var id = UUID.randomUUID().toString();
        commands.sendAndWait(new RecordCount(new CounterId(id), 4L));
        await().atMost(Duration.ofSeconds(10)).untilAsserted(() -> assertTrue(
                queries.query(new HealthyTotals(), HealthyTotals.Result.class).get(5, TimeUnit.SECONDS).rows()
                        .stream().anyMatch(row -> row.id().value().equals(id) && row.amount() == 4L)));
        assertThrows(IllegalStateException.class, () -> unrelated.handle(new UnrelatedOpaqueTotals()));
        assertThrows(IllegalStateException.class, () -> unrelatedPerformer.perform(new UnrelatedOpaqueTotals(), healthy));
        http.perform(get("/api/accounts/totals/healthy-totals").with(user("reader"))).andExpect(status().isOk());
        assertUnavailableHttp("unrelated-opaque-totals", false);
    }

    @Test
    void snapshotAndSseRefuseWithoutExposingRowsOrBypassingAuthorization() throws Exception {
        assertUnavailableHttp("quarantined-totals", false);
        assertUnavailableHttp("live-quarantined-totals", true);
        assertUnavailableHttp("opaque-quarantined-totals", false);
        http.perform(get("/api/accounts/totals/protected-quarantined-totals").with(anonymous())).andExpect(status().isUnauthorized());
        http.perform(get("/api/accounts/totals/protected-quarantined-totals").with(user("reader"))).andExpect(status().isForbidden());
        var response = http.perform(get("/api/accounts/totals/protected-quarantined-totals").with(user("admin").roles("Admin")))
                .andExpect(status().isServiceUnavailable()).andReturn().getResponse();
        assertUnavailableBody(response.getContentAsString());
    }

    private void assertUnavailableHttp(String name, boolean live) throws Exception {
        var request = get("/api/accounts/totals/" + name).with(user("reader"));
        if (live) request.accept("text/event-stream");
        var result = http.perform(request).andExpect(status().isServiceUnavailable()).andReturn();
        assertFalse(result.getRequest().isAsyncStarted(), "No live stream may open");
        assertUnavailableBody(result.getResponse().getContentAsString());
    }

    private void assertUnavailableBody(String body) throws Exception {
        var message = "No implementation details or row content";
        if (body.isEmpty()) return;
        var problem = mapper.readTree(body);
        assertNotNull(problem, message);
        assertTrue(problem.isObject(), message);
        assertTrue(Set.of("type", "title", "status", "instance").containsAll(problem.propertyNames()), message);
        for (var value : problem.values()) assertTrue(value.isValueNode(), message);
        if (problem.has("status")) {
            var status = problem.get("status");
            assertTrue(status.isIntegralNumber() && status.canConvertToInt(), message);
            assertEquals(503, status.intValue(), message);
        }
        var content = problem.toString();
        for (var rowContent : Set.of("retained", "\"id\"", "\"amount\"", "\"value\"", "\"rows\"")) {
            assertFalse(content.contains(rowContent), message);
        }
        assertFalse(content.matches("(?s).*\\b(?:991|4|1)\\b.*"), message);
        assertFalse(content.matches("(?is).*[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}.*"), message);
    }

    private void assertUnavailableSubscription() throws Exception {
        var terminal = new CountDownLatch(1);
        var values = new AtomicInteger();
        var failure = new AtomicReference<Throwable>();
        LiveQuarantinedTotalsSubscription.values(queries, new LiveQuarantinedTotals()).subscribe(new Flow.Subscriber<Object>() {
            public void onSubscribe(Flow.Subscription subscription) { subscription.request(Long.MAX_VALUE); }
            public void onNext(Object value) { values.incrementAndGet(); }
            public void onError(Throwable error) { failure.set(error); terminal.countDown(); }
            public void onComplete() { terminal.countDown(); }
        });
        assertTrue(terminal.await(10, TimeUnit.SECONDS));
        assertNotNull(failure.get(), "A subscription must error, not return an empty or stale snapshot");
        assertEquals(0, values.get());
    }

    private JpaReadModelStore<QuarantinedTotal> retainRow() throws Exception {
        var identity = NAMESPACE + ":java.lang.String:" + mapper.writeValueAsString("retained");
        var digest = HexFormat.of().formatHex(MessageDigest.getInstance("SHA-256").digest(identity.getBytes(StandardCharsets.UTF_8)));
        var manager = factory.createEntityManager();
        try {
            manager.getTransaction().begin();
            manager.createNativeQuery("insert into stage_view_row (id, namespace, identity, payload) values (?1, ?2, ?3, ?4)")
                    .setParameter(1, digest).setParameter(2, NAMESPACE).setParameter(3, identity)
                    .setParameter(4, mapper.writeValueAsString(new QuarantinedTotal(new CounterId("retained"), 991L))).executeUpdate();
            manager.getTransaction().commit();
        } finally { manager.close(); }
        return new JpaReadModelStore<>(factory, mapper, NAMESPACE, QuarantinedTotal.class);
    }
}
