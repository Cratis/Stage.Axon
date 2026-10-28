// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Emitters.Events;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Runtime;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;
using Cratis.Stage.Rendering.Axon.Specifications.Views;

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence;

internal sealed class PersistentViewRemovalProof(AxonEmissionContext context, ViewJavaTypes types, string persistence)
{
    internal void Emit(LocatedSemanticSlice owner, SemanticReadModel model)
    {
        var specs = owner.Slice.Specifications.Where(spec => ViewSpecificationSupport.Legacy(spec, model, types)).ToArray();
        var events = new ProofEvents([.. specs.SelectMany(spec => spec.GivenEvents)], [.. specs.Where(spec => spec.GivenEvents.Length >= 2).Select(spec => spec.GivenEvents[^1])]);
        var scenario = owner.Slice.Projections.Where(projection => projection.ReadModel == model.Id && ProjectionHandlerShape.RootOnly(projection))
            .Select(projection => Scenario(projection, events)).FirstOrDefault(scenario => scenario is not null);
        if (scenario is not null) Emit(owner, model, scenario);
    }

    RemovalCase? Scenario(SemanticProjection projection, ProofEvents events)
    {
        if (projection.Scope is not { } scope) return null;
        return scope.From.SelectMany(from => scope.Removals.Select(removal => (From: from, Removal: removal)))
            .Select(pair => Scenario(projection, events, pair.From, pair.Removal)).FirstOrDefault(scenario => scenario is not null);
    }

    RemovalCase? Scenario(SemanticProjection projection, ProofEvents events, SemanticProjectionFrom from, SemanticProjectionRemoval removal)
    {
        var removed = events.Removals.FirstOrDefault(given => given.EventContract == removal.EventContract);
        if (removed is null || Key(removal.Key, removed) is not { } key) return null;
        var created = events.All.FirstOrDefault(given => given.EventContract == from.EventContract && Key(from.Key, given) == key);
        var other = events.All.FirstOrDefault(given => given.EventContract == from.EventContract && Key(from.Key, given) is { } otherKey && otherKey != key);
        return created is null || other is null ? null : new(projection, created, other, removed, key, Key(from.Key, other)!);
    }

    string? Key(SemanticProjectionKey key, SemanticSpecificationEvent given)
    {
        if (key is not SemanticProjectionPartsKey { Parts.Length: > 0 } parts) return null;
        var properties = context.Request.Model.Application.Types.SelectMany(type => type.Properties)
            .Concat(ScopeSelection.Locate(context.Request.Model.Application).SelectMany(owner => owner.Slice.ReadModels).SelectMany(model => model.Properties))
            .Concat(ScopeSelection.Locate(context.Request.Model.Application).SelectMany(owner => owner.Slice.Events).SelectMany(contract => contract.Properties))
            .GroupBy(property => property.Id).ToDictionary(group => group.Key, group => group.First());
        var entries = new List<string>();
        foreach (var part in parts.Parts)
        {
            if (part.Value is not SemanticProjectionEventProperty { Path.Length: 1 } property || given.Values.SingleOrDefault(value => value.TargetProperty == property.Path[0]) is not { } supplied) return null;
            var target = properties[part.Property].Type;
            var value = types.Unwrap(target, types.Literal(supplied.Value, target));
            entries.Add($"java.util.Map.entry({JavaCodeBuilder.StringLiteral(part.Property.ToString())}, String.valueOf({value}))");
        }
        return $"java.util.Map.ofEntries({string.Join(", ", entries)})";
    }

    void Emit(LocatedSemanticSlice owner, SemanticReadModel model, RemovalCase scenario)
    {
        var (projection, created, other, removed, key, otherKey) = scenario;
        var package = types.Package(owner);
        var modelName = JavaNames.Type(model.Name);
        var name = modelName + "PersistentRemoval";
        var projector = package + "." + JavaNames.Type(projection.Name) + "Projector";
        var create = AxonViewEvents.Expression(context, types, created, source: name + "Test.createSource");
        var secondary = AxonViewEvents.Expression(context, types, other, source: name + "Test.otherSource");
        var remove = AxonViewEvents.Expression(context, types, removed, source: name + "Test.removeSource");
        var tags = new[] { created.EventContract, removed.EventContract }.Distinct().Select(id =>
        {
            var contract = ScopeSelection.Locate(context.Request.Model.Application).SelectMany(owner => owner.Slice.Events).Single(contract => contract.Id == id);
            var tag = AxonEventSources.PrimaryTag(context, contract);
            return $"        if (event instanceof {types.EventType(id)} value) return new org.axonframework.messaging.eventstreaming.Tag({EventTagsEmitter.Reference(context, contract)}, value.{tag.ComponentName}().toString());";
        });
        var imports = new[]
        {
            types.BasePackage + ".infrastructure.AxonConfiguration.class", types.BasePackage + ".infrastructure.SecurityConfiguration.class",
            AxonRuntimePackage.Type(types.BasePackage, "persistent.PersistentConfiguration") + ".class", persistence + ".PersistentViewConfiguration.class",
            package + "." + modelName + "StoreConfiguration.class", projector + ".class"
        };
        var replacements = new Dictionary<string, string>
        {
            ["PACKAGE"] = package,
            ["RUNTIME_MODULE"] = AxonRuntimePackage.ModulePackage,
            ["PERSISTENCE"] = persistence,
            ["APPLICATION"] = types.BasePackage,
            ["MODEL"] = package + "." + modelName,
            ["NAME"] = name,
            ["QUALIFIER"] = package + "." + modelName,
            ["PROJECTOR"] = projector,
            ["SEQUENCE"] = types.EventType(removed.EventContract).Split('.')[^1] + "Sequence",
            ["CREATE_SEQUENCE"] = types.EventType(created.EventContract).Split('.')[^1] + "Sequence",
            ["EVENT_TYPE"] = types.EventType(removed.EventContract),
            ["CREATE_TYPE"] = types.EventType(created.EventContract),
            ["EVENT"] = remove,
            ["ARGUMENT"] = ", null",
            ["REDUCER_ARGUMENT"] = string.Empty,
            ["REDUCER_IMPORTS"] = string.Empty,
            ["IMPORTS"] = string.Join(", ", imports),
            ["NORMALIZE"] = "return row;",
            ["TAGS"] = string.Join('\n', tags),
            ["FIXTURE_EVENTS"] = string.Join(", ", create, secondary),
            ["EXPECTED"] = $"        projector.on({create}, null);\n        projector.on({secondary}, null);",
            ["KEY"] = key,
            ["OTHER_KEY"] = otherKey,
            ["CREATE"] = create,
            ["OTHER"] = secondary,
            ["REMOVE"] = remove,
            ["RECREATE"] = AxonViewEvents.Expression(context, types, created, source: name + "Test.recreateSource")
        };
        void Add(string suffix, string source)
        {
            foreach (var (token, value) in replacements) source = source.Replace("__" + token + "__", value, StringComparison.Ordinal);
            context.Artifacts.Add(PlannedArtifact.CreateText($"src/test/java/{package.Replace('.', '/')}/{name}{suffix}.java", PersistentJavaTemplate.Render(source), [model.Id, projection.Id]));
        }
        Add("Support", PersistentViewProofSupport.Source);
        Add("Fixture", PersistentViewFixtureContract.Source);
        Add("Test", Template.Source);
        Add("StoreTest", PersistentViewContractTests.Source);
    }

    sealed record ProofEvents(SemanticSpecificationEvent[] All, SemanticSpecificationEvent[] Removals);

    sealed record RemovalCase(SemanticProjection Projection, SemanticSpecificationEvent Created, SemanticSpecificationEvent Other, SemanticSpecificationEvent Removed, string Key, string OtherKey);

    internal static class Template
    {
        internal const string Source = """
        package __PACKAGE__;

        import __PERSISTENCE__.JpaReadModelStore;
        import java.nio.file.Path;
        import java.util.Base64;
        import java.util.HashMap;
        import java.util.Map;
        import java.util.UUID;
        import java.util.concurrent.CountDownLatch;
        import java.util.concurrent.TimeUnit;
        import org.axonframework.common.configuration.Configuration;
        import org.axonframework.conversion.GeneralConverter;
        import org.axonframework.eventsourcing.eventstore.EventStore;
        import org.axonframework.eventsourcing.eventstore.AggregateBasedEventStorageEngineUtils;
        import org.axonframework.messaging.eventhandling.processing.streaming.StreamingEventProcessor;
        import org.axonframework.messaging.eventhandling.processing.streaming.token.TrackingToken;
        import org.axonframework.messaging.eventhandling.processing.streaming.token.WrappedToken;
        import org.axonframework.messaging.eventhandling.processing.streaming.token.store.jpa.TokenEntry;
        import org.axonframework.messaging.core.annotation.Namespace;
        import org.axonframework.messaging.core.annotation.SequencingPolicy;
        import org.axonframework.messaging.core.unitofwork.ProcessingContext;
        import org.axonframework.messaging.eventhandling.annotation.EventHandler;
        import org.axonframework.messaging.eventhandling.processing.streaming.segmenting.Segment;
        import org.junit.jupiter.api.Test;
        import org.springframework.boot.test.context.TestConfiguration;
        import org.springframework.context.ConfigurableApplicationContext;
        import org.springframework.context.annotation.Bean;
        import static org.junit.jupiter.api.Assertions.*;
        import static __PACKAGE__.__NAME__Support.*;
        import static __PACKAGE__.__NAME__Fixture.assertFixture;
        import static __PACKAGE__.__NAME__Fixture.tag;

        /** Removal travels through the real emitted handler, pooled processor and JpaTokenStore. */
        class __NAME__Test {
            static final Object key = __KEY__;
            static final Object otherKey = __OTHER_KEY__;
            static final java.util.List<Segment> routing = Segment.splitBalanced(Segment.ROOT_SEGMENT, 3);
            static final Segment rowSegment = routing.stream().filter(segment -> segment.matches(key)).findFirst().orElseThrow();
            static final Segment heldSegment = routing.stream().filter(segment -> !segment.equals(rowSegment)).findFirst().orElseThrow();
            static final String createSource = sourceFor(heldSegment);
            static final String removeSource = sourceFor(rowSegment);
            static final String otherSource = "other-row";
            static final String recreateSource = "recreated-row";
            static volatile boolean holdCreate;
            static volatile boolean failHandler;
            static volatile boolean failToken;
            static volatile CountDownLatch entered;
            static volatile CountDownLatch release;
            static volatile CountDownLatch deleted;
            static final Map<Object, Integer> owners = new java.util.concurrent.ConcurrentHashMap<>();

            @Test void removesAndRecreatesWithOneSegment() throws Exception { lifecycle(1); }
            @Test void removesAndRecreatesWithFourSegments() throws Exception { lifecycle(4); }
            @Test void handlerFailureRollsBackWithOneSegment() throws Exception { rollback(1, false); }
            @Test void handlerFailureRollsBackWithFourSegments() throws Exception { rollback(4, false); }
            @Test void tokenFailureRollsBackWithOneSegment() throws Exception { rollback(1, true); }
            @Test void tokenFailureRollsBackWithFourSegments() throws Exception { rollback(4, true); }

            @Test void differentDefaultSegmentsCreateThenRemoveOneRowInStreamOrder() throws Exception {
                reset();
                assertTrue(heldSegment.matches(AggregateBasedEventStorageEngineUtils.resolveAggregateIdentifier(java.util.Set.of(tag(__CREATE__)))));
                assertTrue(rowSegment.matches(AggregateBasedEventStorageEngineUtils.resolveAggregateIdentifier(java.util.Set.of(tag(__REMOVE__)))));
                assertNotEquals(heldSegment, rowSegment, "FIXTURE: default source routing must invert the competing deliveries");
                try (var application = boot(database(), 4)) {
                    append(application, __OTHER__);
                    catchUp(application, head(application), otherKey);
                    append(application, __CREATE__);
                    var initialHead = head(application);
                    catchUp(application, initialHead, key);
                    var seedOwner = owners.get(key);
                    var processor = application.getBean(Configuration.class).getComponents(StreamingEventProcessor.class).get("__PACKAGE__");
                    assertNotNull(processor);
                    await(() -> processor.processingStatus().size() == 4);
                    processor.releaseSegment(heldSegment.getSegmentId(), 1, TimeUnit.DAYS).get(10, TimeUnit.SECONDS);
                    await(() -> !processor.processingStatus().containsKey(heldSegment.getSegmentId()));
                    append(application, __CREATE__);
                    var heldHead = head(application);
                    append(application, __REMOVE__);
                    var terminalHead = head(application);
                    await(() -> coversHead(application, terminalHead, rowSegment.getSegmentId()));
                    assertFalse(processor.processingStatus().containsKey(heldSegment.getSegmentId()));
                    assertTrue(processor.claimSegment(heldSegment.getSegmentId()).get(10, TimeUnit.SECONDS));
                    await(() -> processor.processingStatus().containsKey(heldSegment.getSegmentId()));
                    await(() -> coversHead(application, heldHead, seedOwner));
                    assertTrue(store(application).get(key).isEmpty(), "Without row routing the reclaimed create would resurrect the removed row");
                    assertTrue(store(application).get(otherKey).isPresent());
                    assertEquals(1, count(application));
                }
            }

            @Test void legacyProjectorStillRemovesOnlyTheSelectedRow() {
                assertNotEquals(createSource, removeSource);
                var memory = new __RUNTIME_MODULE__.InMemoryReadModelStore<Object, __MODEL__>();
                var projector = projector(memory);
                projector.on(__CREATE__, null);
                projector.on(__OTHER__, null);
                var remaining = memory.get(otherKey).orElseThrow();
                projector.on(__REMOVE__, null);
                assertTrue(memory.get(key).isEmpty());
                assertEquals(remaining, memory.get(otherKey).orElseThrow());
                var inverted = new __RUNTIME_MODULE__.InMemoryReadModelStore<Object, __MODEL__>();
                projector(inverted).on(__REMOVE__, null);
                projector(inverted).on(__CREATE__, null);
                assertTrue(inverted.get(key).isPresent(), "FIXTURE: inversion must be observable");
                projector.on(__RECREATE__, null);
                assertEquals(2, memory.all().size());
            }

            private void lifecycle(int segments) throws Exception {
                reset();
                var path = database();
                Map<Integer, String> checkpoint;
                Map<String, String> secondary;
                TrackingToken removalHead;
                TrackingToken recreatedHead;
                Map<Integer, String> recreatedCheckpoint;
                Map<Integer, TrackingToken> decodedCheckpoint;
                Map<Integer, TrackingToken> decodedRecreated;
                int removalOwner;
                try (var application = boot(path, segments)) {
                    var before = tokens(application);
                    append(application, __OTHER__);
                    catchUp(application, head(application), otherKey);
                    var other = store(application).get(otherKey).orElseThrow();
                    secondary = rawRows(application);
                    var claimed = claims(application);
                    holdCreate = true;
                    append(application, __CREATE__);
                    assertTrue(entered.await(10, TimeUnit.SECONDS), "Earlier create must enter before removal append");
                    append(application, __REMOVE__);
                    removalHead = head(application);
                    assertFalse(deleted.await(250, TimeUnit.MILLISECONDS), "Same-row removal must wait for the earlier create, despite distinct event sources");
                    assertEquals(claimed, claims(application), "Keep stable owners while the create barrier is held");
                    assertEquals(other, store(application).get(otherKey).orElseThrow());
                    release.countDown();
                    assertTrue(deleted.await(10, TimeUnit.SECONDS));
                    removalOwner = observedOwner;
                    await(() -> coversHead(application, removalHead, removalOwner));
                    assertTrue(store(application).get(key).isEmpty());
                    assertEquals(1, count(application));
                    assertEquals(other, store(application).get(otherKey).orElseThrow());
                    assertEquals(secondary, rawRows(application), "Untargeted row payload must remain byte-for-byte unchanged");
                    assertEquals(owners.get(key), observedOwner, "Create and removal must share their actual row-policy owner");
                    assertFixture(application, before);
                    checkpoint = tokens(application);
                    assertEquals(segments, checkpoint.size());
                    assertNotEquals("<null>", checkpoint.get(removalOwner));
                    decodedCheckpoint = decodedTokens(application);
                } finally { release.countDown(); }
                try (var application = boot(path, segments)) {
                    await(() -> coversHead(application, removalHead, removalOwner));
                    assertTrue(store(application).get(key).isEmpty(), "Deletion must survive restart without replay");
                    assertTrue(store(application).get(otherKey).isPresent());
                    assertEquals(secondary, rawRows(application));
                    assertUnchangedOwner(application, removalOwner, checkpoint, decodedCheckpoint);
                    assertEquals(1, count(application));
                    append(application, __RECREATE__);
                    recreatedHead = head(application);
                    catchUp(application, recreatedHead, key);
                    assertEquals(new java.util.HashSet<>(expected()), new java.util.HashSet<>(store(application).all()));
                    assertEquals(1, count(application), "Recreation is a create, not a removal replay");
                    recreatedCheckpoint = tokens(application);
                    decodedRecreated = decodedTokens(application);
                }
                try (var application = boot(path, segments)) {
                    await(() -> coversHead(application, recreatedHead, removalOwner));
                    assertEquals(new java.util.HashSet<>(expected()), new java.util.HashSet<>(store(application).all()));
                    assertUnchangedOwner(application, removalOwner, recreatedCheckpoint, decodedRecreated);
                    assertEquals(1, count(application), "Caught-up restart must never redeliver removal after recreation");
                }
            }

            private void rollback(int segments, boolean tokenFailure) throws Exception {
                reset();
                var path = database();
                Map<Integer, String> checkpoint;
                java.util.List<__MODEL__> initial;
                Map<String, String> secondary;
                Map<String, String> original;
                TrackingToken removalHead;
                int owner;
                try (var application = boot(path, segments)) {
                    var before = tokens(application);
                    append(application, __OTHER__);
                    catchUp(application, head(application), otherKey);
                    secondary = rawRows(application);
                    append(application, __CREATE__);
                    catchUp(application, head(application), key);
                    assertFixture(application, before);
                    initial = store(application).all();
                    original = rawRows(application);
                    checkpoint = tokens(application);
                    var decodedCheckpoint = decodedTokens(application);
                    owner = owners.get(key);
                    assertEquals(segments, checkpoint.size());
                    assertFalse(checkpoint.get(owner).equals("<null>"));
                    if (tokenFailure) installTokenFailure(path, owner);
                    failToken = tokenFailure;
                    failHandler = !tokenFailure;
                    append(application, __REMOVE__);
                    removalHead = head(application);
                    assertTrue(deleted.await(10, TimeUnit.SECONDS), "Actual generated removal must delete inside its pooled handler");
                    if (tokenFailure) assertTrue(tokenRejected.await(10, TimeUnit.SECONDS), "Real TokenStore persistence must reject the owning segment");
                    assertTrue(rolledBack.await(10, TimeUnit.SECONDS));
                    assertEquals(owner, observedOwner);
                    assertEquals(new java.util.HashSet<>(initial), new java.util.HashSet<>(store(application).all()), "Failed removal must restore both original rows");
                    assertEquals(original, rawRows(application), "Rollback must preserve both original payloads byte-for-byte");
                    assertEquals(0, count(application), "The removal witness must roll back too");
                    assertUnchangedOwner(application, owner, checkpoint, decodedCheckpoint);
                }
                reset();
                Map<Integer, String> committed;
                Map<Integer, TrackingToken> decodedCommitted;
                try (var application = boot(path, segments)) {
                    assertTrue(deleted.await(10, TimeUnit.SECONDS), "Restart must reclaim and retry the same removal");
                    await(() -> coversHead(application, removalHead, owner));
                    assertTrue(store(application).get(key).isEmpty());
                    assertEquals(1, count(application));
                    assertNotEquals(decoded(checkpoint.get(owner)), decoded(tokens(application).get(owner)));
                    assertEquals(1, store(application).all().size());
                    assertTrue(store(application).get(otherKey).isPresent(), "Other row must survive rollback and reclaim");
                    assertEquals(secondary, rawRows(application));
                    committed = tokens(application);
                    decodedCommitted = decodedTokens(application);
                }
                try (var application = boot(path, segments)) {
                    await(() -> coversHead(application, removalHead, owner));
                    assertTrue(store(application).get(key).isEmpty());
                    assertTrue(store(application).get(otherKey).isPresent());
                    assertEquals(secondary, rawRows(application));
                    assertUnchangedOwner(application, owner, committed, decodedCommitted);
                    assertEquals(1, count(application), "The successful removal must commit exactly once");
                }
            }

            // Same committed-owner, position-based barrier as the #65 row-order proof.
            private static String sourceFor(Segment segment) {
                for (var index = 0; index < 1000; index++) {
                    var source = "row-source-" + index;
                    if (segment.matches(source)) return source;
                }
                throw new AssertionError("FIXTURE: no source for " + segment);
            }
            private static TrackingToken head(ConfigurableApplicationContext application) throws Exception {
                var events = application.getBean(Configuration.class).getComponent(EventStore.class);
                return units(application).create().executeWithResult(events::latestToken).get(10, TimeUnit.SECONDS);
            }
            private static boolean coversHead(ConfigurableApplicationContext application, TrackingToken head, int owner) {
                assertNotNull(head, "FIXTURE: appended stream must have a head");
                var converter = application.getBean(Configuration.class).getComponent(GeneralConverter.class);
                try (var manager = application.getBean(jakarta.persistence.EntityManagerFactory.class).createEntityManager()) {
                    var entry = manager.createQuery("select e from TokenEntry e where e.processorName = :name", TokenEntry.class)
                            .setParameter("name", "__PACKAGE__").getResultList().stream()
                            .filter(candidate -> candidate.getSegment().getSegmentId() == owner).findFirst().orElse(null);
                    if (entry == null) return false;
                    var token = entry.getToken(converter);
                    return token != null && reachedPosition(WrappedToken.unwrapLowerBound(token), head) && reachedPosition(WrappedToken.unwrapUpperBound(token), head);
                }
            }
            private static boolean reachedPosition(TrackingToken token, TrackingToken head) {
                if (token == null) return false;
                var position = token.position();
                var headPosition = head.position();
                // Do not wait for cleanup of historical gaps in a committed owner's token.
                return position.isPresent() && headPosition.isPresent() && position.getAsLong() >= headPosition.getAsLong();
            }
            private static Map<Integer, TrackingToken> decodedTokens(ConfigurableApplicationContext application) {
                var converter = application.getBean(Configuration.class).getComponent(GeneralConverter.class);
                try (var manager = application.getBean(jakarta.persistence.EntityManagerFactory.class).createEntityManager()) {
                    var result = new java.util.TreeMap<Integer, TrackingToken>();
                    for (var entry : manager.createQuery("select e from TokenEntry e where e.processorName = :name", TokenEntry.class)
                            .setParameter("name", "__PACKAGE__").getResultList()) result.put(entry.getSegment().getSegmentId(), entry.getToken(converter));
                    return result;
                }
            }
            private static void assertUnchangedOwner(ConfigurableApplicationContext application, int owner,
                    Map<Integer, String> checkpoint, Map<Integer, TrackingToken> decodedCheckpoint) {
                var after = tokens(application);
                assertEquals(checkpoint.keySet(), after.keySet());
                assertEquals(checkpoint.get(owner), after.get(owner), "Caught-up owning token must stay byte-identical");
                var decodedAfter = decodedTokens(application);
                assertEquals(checkpoint.keySet(), decodedAfter.keySet());
                for (var segment : checkpoint.keySet()) {
                    if (segment == owner) continue;
                    assertCovers(WrappedToken.unwrapLowerBound(decodedAfter.get(segment)), WrappedToken.unwrapLowerBound(decodedCheckpoint.get(segment)));
                    assertCovers(WrappedToken.unwrapUpperBound(decodedAfter.get(segment)), WrappedToken.unwrapUpperBound(decodedCheckpoint.get(segment)));
                }
            }
            private static void assertCovers(TrackingToken after, TrackingToken before) {
                if (before == null) return;
                assertNotNull(after, "Non-owner checkpoint must not regress to null");
                assertTrue(after.covers(before), "Non-owner checkpoint must cover its prior committed position");
            }
            private static void catchUp(ConfigurableApplicationContext application, TrackingToken head, Object key) throws Exception {
                await(() -> owners.containsKey(key) && coversHead(application, head, owners.get(key)));
            }
            private static String decoded(String token) {
                assertNotNull(token);
                assertNotEquals("<null>", token);
                return new String(Base64.getDecoder().decode(token), java.nio.charset.StandardCharsets.UTF_8);
            }
            private static void reset() {
                holdCreate = false; failHandler = false; failToken = false; failure = Failure.NONE;
                entered = new CountDownLatch(1); release = new CountDownLatch(1); deleted = new CountDownLatch(1);
                rolledBack = new CountDownLatch(1); tokenRejected = new CountDownLatch(1);
                owners.clear(); observedOwner = null;
            }
            private static ConfigurableApplicationContext boot(Path path, int segments) throws Exception {
                var application = new org.springframework.boot.builder.SpringApplicationBuilder(FlatFixture.class)
                        .sources(WitnessConfiguration.class).profiles("persistent").run("--server.port=0",
                        "--spring.datasource.url=jdbc:h2:file:" + path, "--spring.jpa.hibernate.ddl-auto=update",
                        "--axon.eventhandling.processors[__PACKAGE__].initial-segment-count=" + segments);
                try { await(() -> claims(application).size() == segments); return application; }
                catch (Exception | Error cause) { application.close(); throw cause; }
            }
            private static Map<String, String> rawRows(ConfigurableApplicationContext application) {
                try (var manager = application.getBean(jakarta.persistence.EntityManagerFactory.class).createEntityManager()) {
                    var result = new java.util.TreeMap<String, String>();
                    for (var row : manager.createQuery("select e.identity, e.payload from StageViewRow e where e.namespace = :name", Object[].class)
                            .setParameter("name", "__QUALIFIER__").getResultList()) result.put((String) row[0], (String) row[1]);
                    return result;
                }
            }
            private static Map<Integer, String> claims(ConfigurableApplicationContext application) {
                try (var manager = application.getBean(jakarta.persistence.EntityManagerFactory.class).createEntityManager()) {
                    var result = new HashMap<Integer, String>();
                    for (var row : manager.createQuery("select e.segment, e.owner from TokenEntry e where e.processorName = :name", Object[].class)
                            .setParameter("name", "__PACKAGE__").getResultList()) {
                        if (row[1] != null) result.put(((Segment) row[0]).getSegmentId(), (String) row[1]);
                    }
                    return result;
                }
            }
            @TestConfiguration(proxyBeanMethods = false)
            static class WitnessConfiguration {
                @Bean RemovalWitness removalWitness(ConfigurableApplicationContext application) { return new RemovalWitness(application); }
            }
            @Namespace("__PACKAGE__")
            static class RemovalWitness {
                private final ConfigurableApplicationContext application;
                RemovalWitness(ConfigurableApplicationContext application) { this.application = application; }
                @EventHandler
                @SequencingPolicy(type = __PROJECTOR__.__CREATE_SEQUENCE__.class)
                public void create(__CREATE_TYPE__ event, ProcessingContext context) throws Exception {
                    var owner = Segment.fromContext(context).orElseThrow().getSegmentId();
                    if (event.eventSourceId().toString().equals(otherSource)) owners.put(otherKey, owner);
                    else owners.put(key, owner);
                    if (holdCreate && event.eventSourceId().toString().equals(createSource)) {
                        entered.countDown();
                        assertTrue(release.await(10, TimeUnit.SECONDS), "Release earlier create");
                    }
                }
                @EventHandler
                @SequencingPolicy(type = __PROJECTOR__.__SEQUENCE__.class)
                public void remove(__EVENT_TYPE__ event, ProcessingContext context) {
                    observedOwner = Segment.fromContext(context).orElseThrow().getSegmentId();
                    context.onError((ignored, phase, cause) -> rolledBack.countDown());
                    var bound = JpaReadModelStore.bind(store(application), context);
                    var other = bound.get(otherKey).orElseThrow();
                    projector(bound).on(event, null);
                    assertTrue(bound.get(key).isEmpty(), "Flush the actual generated delete before failure injection");
                    assertEquals(other, bound.get(otherKey).orElseThrow());
                    if (failToken) failure = Failure.TOKEN;
                    deleted.countDown();
                    if (failHandler) throw new IllegalStateException("deliberate handler failure after actual removal");
                }
            }
        }
        """;
    }
}
