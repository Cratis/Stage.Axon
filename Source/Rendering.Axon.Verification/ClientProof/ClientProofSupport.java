package __RUNTIME__;

import __APPLICATION__.work.items.overview.WorkItemById;
import __APPLICATION__.work.items.overview.WorkItems;
import __APPLICATION__.work.items.overview.WorkItemsEntity;
import __APPLICATION__.work.items.overview.WorkItemsRepository;
import __RUNTIME_MODULE__.LiveStreams;
import jakarta.servlet.DispatcherType;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.io.OutputStreamWriter;
import java.io.Writer;
import java.lang.reflect.InvocationTargetException;
import java.lang.reflect.Method;
import java.lang.reflect.Proxy;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CopyOnWriteArrayList;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.atomic.AtomicReference;
import org.axonframework.common.configuration.Configuration;
import org.axonframework.messaging.core.unitofwork.UnitOfWorkFactory;
import org.axonframework.messaging.queryhandling.QueryUpdateEmitter;
import org.springframework.beans.factory.config.BeanPostProcessor;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.context.annotation.Bean;
import org.springframework.web.servlet.HandlerInterceptor;
import org.springframework.web.servlet.config.annotation.InterceptorRegistry;
import org.springframework.web.servlet.config.annotation.WebMvcConfigurer;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.junit.jupiter.api.Assertions.fail;

/**
 * Runs the generated TypeScript client against this running application: each scenario is a Node process that uses only the generated
 * client, and asks this side to act (commit, roll back, hold a read, count reads) through a line protocol on stdin/stdout:
 * the scenario prints {@code await:<label>} and waits for {@code go:<label> [payload]}.
 */
final class ClientProofSupport {
    static final String VIEW = "__APPLICATION__.work.items.overview.WorkItemsRepository";
    static final AtomicReference<Gate> GATE = new AtomicReference<>();
    static final AtomicInteger READS = new AtomicInteger();
    static final List<String> CALLERS = new CopyOnWriteArrayList<>();
    private static final long WAIT = 20;

    private ClientProofSupport() {}

    /** Holds one read of the work item view: before it reads, or after it has read (so the stale rows are delivered late). */
    record Gate(boolean afterRead, CountDownLatch entered, CountDownLatch release) {}

    /** Wraps the work item repository so a test can hold and count its reads; every other behaviour is the real repository. */
    @TestConfiguration(proxyBeanMethods = false)
    static class Probes {
        @Bean
        static BeanPostProcessor gatedWorkItems() {
            return new BeanPostProcessor() {
                @Override
                public Object postProcessAfterInitialization(Object bean, String name) {
                    return VIEW.equals(name) && bean instanceof WorkItemsRepository repository ? gated(repository) : bean;
                }
            };
        }

        @Bean
        WebMvcConfigurer recordedCallers() {
            return new WebMvcConfigurer() {
                @Override
                public void addInterceptors(InterceptorRegistry registry) {
                    registry.addInterceptor(new HandlerInterceptor() {
                        @Override
                        public boolean preHandle(HttpServletRequest request, HttpServletResponse response, Object handler) {
                            if (request.getDispatcherType() == DispatcherType.REQUEST && request.getRequestURI().endsWith("/create-work-item")) {
                                var principal = request.getUserPrincipal();
                                CALLERS.add(request.getHeader("X-User") + ">" + (principal == null ? "anonymous" : principal.getName()));
                            }
                            return true;
                        }
                    });
                }
            };
        }
    }

    static WorkItemsRepository gated(WorkItemsRepository inner) {
        return (WorkItemsRepository) Proxy.newProxyInstance(WorkItemsRepository.class.getClassLoader(), new Class<?>[] { WorkItemsRepository.class }, (proxy, method, arguments) -> {
            if (!method.getName().equals("findAll")) return invoke(inner, method, arguments);
            READS.incrementAndGet();
            var gate = GATE.getAndSet(null);
            if (gate == null) return invoke(inner, method, arguments);
            if (!gate.afterRead()) hold(gate);
            var rows = invoke(inner, method, arguments);
            if (gate.afterRead()) hold(gate);
            return rows;
        });
    }

    private static Object invoke(WorkItemsRepository inner, Method method, Object[] arguments) throws Throwable {
        try {
            return method.invoke(inner, arguments);
        } catch (InvocationTargetException failure) { throw failure.getCause(); }
    }

    private static void hold(Gate gate) {
        gate.entered().countDown();
        try {
            if (!gate.release().await(30, TimeUnit.SECONDS)) throw new IllegalStateException("The gate was never released");
        } catch (InterruptedException interrupted) {
            Thread.currentThread().interrupt();
            throw new IllegalStateException(interrupted);
        }
    }

    /** What the Java side does when a scenario asks. */
    static final class Actions {
        private final Configuration configuration;
        private final WorkItemsRepository store;
        private final LiveStreams live;
        private volatile Gate armed;
        private volatile CompletableFuture<Void> burst = CompletableFuture.completedFuture(null);

        Actions(Configuration configuration, WorkItemsRepository store, LiveStreams live) {
            this.configuration = configuration;
            this.store = store;
            this.live = live;
        }

        void reset() throws InterruptedException {
            GATE.set(null);
            READS.set(0);
            CALLERS.clear();
            store.deleteAll();
            awaitActive(0);
        }

        String perform(String label) throws Exception {
            var parts = label.split(":", 4);
            return switch (parts[0]) {
                case "arm" -> { armed = new Gate("after".equals(parts[1]), new CountDownLatch(1), new CountDownLatch(1)); GATE.set(armed); yield ""; }
                case "held" -> { assertTrue(armed.entered().await(WAIT, TimeUnit.SECONDS), "The read never reached the gate"); yield ""; }
                case "release" -> { armed.release().countDown(); yield ""; }
                case "release-commit" -> {
                    assertTrue(armed.entered().await(WAIT, TimeUnit.SECONDS), "The read never reached the gate");
                    commit(parts[1], parts[2]);
                    armed.release().countDown();
                    yield "";
                }
                case "commit" -> { commit(parts[1], parts[2]); yield ""; }
                case "rollback" -> { rollBack(parts[1], parts[2]); yield ""; }
                case "duplicate" -> { put("copy", get(parts[1])); yield ""; }
                case "active" -> { awaitActive(Integer.parseInt(parts[1])); yield ""; }
                case "reads" -> String.valueOf(READS.get());
                case "callers" -> String.join(",", CALLERS);
                case "burst" -> { startBurst(parts[1], Integer.parseInt(parts[2])); yield ""; }
                case "burst-done" -> { burst.get(WAIT, TimeUnit.SECONDS); yield ""; }
                default -> throw new IllegalArgumentException("Unknown scenario request: " + label);
            };
        }

        private void startBurst(String id, int count) {
            burst = CompletableFuture.runAsync(() -> {
                try {
                    for (var index = 1; index <= count; index++) commit(id, "burst-" + index);
                } catch (Exception failure) { throw new IllegalStateException(failure); }
            });
        }

        /** Commits a new title and announces the change, the way the generated projector does, in one unit of work. */
        private void commit(String id, String title) throws Exception {
            configuration.getComponent(UnitOfWorkFactory.class).create().executeWithResult(processing -> {
                processing.runOnCommit(ignored -> update(id, title));
                QueryUpdateEmitter.forContext(processing).emit(WorkItemById.class, query -> true, WorkItemById.Result::invalidation);
                return CompletableFuture.completedFuture(null);
            }).get(WAIT, TimeUnit.SECONDS);
        }

        /**
         * Writes a change and announces it inside a unit of work that then fails. The write is visible in the store before the
         * outcome is known, so a server that publishes the announcement before the rollback would re-read and deliver the
         * rolled-back value. The original row is put back after a window long enough for such a leak to reach a watcher.
         */
        private void rollBack(String id, String title) throws Exception {
            var original = get(id);
            var failed = configuration.getComponent(UnitOfWorkFactory.class).create().executeWithResult(processing -> {
                update(id, title);
                QueryUpdateEmitter.forContext(processing).emit(WorkItemById.class, query -> true, WorkItemById.Result::invalidation);
                return CompletableFuture.<Object>failedFuture(new IllegalStateException("rollback"));
            });
            assertThrows(ExecutionException.class, () -> failed.get(WAIT, TimeUnit.SECONDS));
            Thread.sleep(500);
            put(id, original);
        }

        private WorkItems get(String id) { return store.findById(id).map(WorkItemsEntity::toModel).orElseThrow(); }

        private void put(String id, WorkItems row) { store.save(new WorkItemsEntity(id, row)); }

        private void update(String id, String title) { put(id, get(id).withTitle(title)); }

        private void awaitActive(int expected) throws InterruptedException {
            var deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(10);
            while (live.active() != expected && System.nanoTime() < deadline) Thread.sleep(25);
            assertEquals(expected, live.active());
        }
    }

    /** Runs one scenario script. A missing Node fails the test: the proof must never be skipped. */
    static void run(String scenario, int port, Actions actions) throws Exception {
        var script = Path.of("client", "test", "e2e", scenario + ".ts");
        assertTrue(Files.exists(script), "Missing generated scenario " + script.toAbsolutePath());
        var builder = new ProcessBuilder("node", script.toString(), "--base", "http://127.0.0.1:" + port, "--heartbeat-ms", "200").redirectErrorStream(true);
        Process process;
        try {
            process = builder.start();
        } catch (IOException missing) {
            throw new AssertionError("Node is required to run the generated client proof but could not be started: " + missing.getMessage(), missing);
        }

        var log = new StringBuilder();
        var failure = new AtomicReference<Throwable>();
        var reader = new Thread(() -> converse(process, actions, log, failure), "client-proof-" + scenario);
        reader.setDaemon(true);
        reader.start();
        if (!process.waitFor(90, TimeUnit.SECONDS)) {
            process.destroyForcibly();
            reader.join(5000);
            fail(scenario + " did not finish within 90s\n" + log);
        }

        reader.join(10_000);
        if (failure.get() != null) throw new AssertionError(scenario + ": the Java side failed a request\n" + log, failure.get());
        assertEquals(0, process.exitValue(), scenario + " failed\n" + log);
    }

    private static void converse(Process process, Actions actions, StringBuilder log, AtomicReference<Throwable> failure) {
        try (var lines = new BufferedReader(new InputStreamReader(process.getInputStream(), StandardCharsets.UTF_8));
             Writer replies = new OutputStreamWriter(process.getOutputStream(), StandardCharsets.UTF_8)) {
            String line;
            while ((line = lines.readLine()) != null) {
                synchronized (log) { log.append(line).append('\n'); }
                if (!line.startsWith("await:")) continue;
                var label = line.substring("await:".length());
                var payload = actions.perform(label);
                replies.write("go:" + label + (payload.isEmpty() ? "" : " " + payload) + "\n");
                replies.flush();
            }
        } catch (Throwable thrown) {
            failure.set(thrown);
            process.destroyForcibly();
        }
    }

}
