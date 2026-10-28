// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Runtime.for_AxonRuntimeEmitter;

public class when_reading_the_runtime_module_sources
{
    [Theory]
    [InlineData("validation")]
    [InlineData("conflict")]
    [InlineData("invalid-argument")]
    [InlineData("unsupported")]
    [InlineData("query-result-missing")]
    [InlineData("query-cardinality")]
    void should_use_a_stable_problem_type(string type) => Source("ProblemDetailsAdvice").ShouldContain($"\"{type}\"");

    [Fact] void should_use_the_urn_prefix() => Source("ProblemDetailsAdvice").ShouldContain("urn:cratis:stage-axon:problem:");
    [Fact] void should_render_framework_errors_as_problem_details() => Source("ProblemDetailsAdvice").ShouldContain("extends ResponseEntityExceptionHandler");
    [Fact] void should_map_cardinality_failures() => Source("ProblemDetailsAdvice").ShouldContain("@ExceptionHandler(QueryCardinalityException.class)");
    [Fact] void should_answer_a_full_server_with_a_problem() => Source("ProblemDetailsAdvice").ShouldContain("live-capacity");

    [Theory]
    [InlineData("Exception")]
    [InlineData("RuntimeException")]
    [InlineData("Throwable")]
    void should_not_swallow_security_exceptions_with_a_catch_all(string type) => Source("ProblemDetailsAdvice").ShouldNotContain($"@ExceptionHandler({type}.class)");

    [Theory]
    [InlineData("QueryResults")]
    [InlineData("QueryCardinalityException")]
    [InlineData("ProblemDetailsAdvice")]
    [InlineData("SseBridge")]
    [InlineData("LiveSnapshots")]
    [InlineData("LiveStreams")]
    [InlineData("LiveCapacityException")]
    [InlineData("ReadModelStore")]
    [InlineData("InMemoryReadModelStore")]
    [InlineData("ReadModelTransaction")]
    void should_declare_the_runtime_module_package(string name) => Source(name).ShouldContain("package io.cratis.stage.axon.runtime;");

    [Theory]
    [InlineData("QueryResults")]
    [InlineData("LiveSnapshots")]
    [InlineData("LiveStreams")]
    [InlineData("LiveCapacityException")]
    [InlineData("SseBridge")]
    [InlineData("ReadModelStore")]
    [InlineData("InMemoryReadModelStore")]
    [InlineData("ReadModelTransaction")]
    void should_not_carry_a_license_header(string name) => Source(name).ShouldNotContain("Copyright");

    [Theory]
    [InlineData("UniqueClaim")]
    [InlineData("JpaUniqueValueStore")]
    [InlineData("PersistentConfiguration")]
    [InlineData("PersistentReadModelSafety")]
    [InlineData("StageAxonPersistenceAutoConfiguration")]
    void should_declare_the_persistent_module_package(string name) => PersistentSource(name).ShouldContain("package io.cratis.stage.axon.runtime.persistent;");

    [Theory]
    [InlineData("UniqueClaim")]
    [InlineData("JpaUniqueValueStore")]
    [InlineData("PersistentConfiguration")]
    [InlineData("PersistentReadModelSafety")]
    void should_not_leave_the_runtime_placeholder_in_the_persistent_helpers(string name) => PersistentSource(name).ShouldNotContain("__RUNTIME__");

    [Fact] void should_keep_the_claim_table_name() => PersistentSource("UniqueClaim").ShouldContain("@Table(name = \"stage_unique_claim\")");
    [Fact] void should_keep_the_persistent_profile() => PersistentSource("PersistentConfiguration").ShouldContain("@Profile(\"persistent\")");
    [Fact] void should_register_the_persistent_auto_configuration() => File.ReadAllText(Path.Combine(ModuleRoot(), "main", "resources", "META-INF", "spring", "org.springframework.boot.autoconfigure.AutoConfiguration.imports")).ShouldContain("io.cratis.stage.axon.runtime.persistent.StageAxonPersistenceAutoConfiguration");

    [Fact] void should_not_leave_the_runtime_placeholder() => Source("QueryResults").ShouldNotContain("__RUNTIME__");
    [Fact] void should_require_exactly_one() => Source("QueryResults").ShouldContain("public static <T> List<T> exactlyOne(List<T> rows)");
    [Fact] void should_allow_at_most_one() => Source("QueryResults").ShouldContain("public static <T> List<T> atMostOne(List<T> rows)");
    [Fact] void should_not_put_row_data_in_the_exception_message() => Source("QueryCardinalityException").ShouldContain("super(\"Expected \" + cardinality + \" query result\");");
    [Fact] void should_keep_the_publisher_overload() => Source("SseBridge").ShouldContain("public static <T> SseEmitter stream(Flow.Publisher<T> publisher)");
    [Fact] void should_queue_a_comment_so_initialisation_commits_the_response() => Source("SseBridge").ShouldContain("emitter.send(SseEmitter.event().comment(\"open\"));");
    [Fact] void should_queue_the_comment_before_delivery_starts() => Precedes(Source("SseBridge"), "commitOnInitialise(emitter);", "publisher.subscribe(subscriber);").ShouldBeTrue();

    static bool Precedes(string text, string first, string second)
    {
        var index = text.IndexOf(first, StringComparison.Ordinal);
        return index >= 0 && index < text.IndexOf(second, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ViewRow")]
    [InlineData("ViewGeneration")]
    [InlineData("JpaReadModelStore")]
    [InlineData("PersistentViewBootstrap")]
    [InlineData("PersistentViewProcessorGuard")]
    void should_publish_fixed_view_helpers(string name)
    {
        var source = ViewsSource(name);
        source.ShouldContain("package io.cratis.stage.axon.runtime.views;");
        source.ShouldNotContain("__RUNTIME_MODULE__");
        source.ShouldNotContain("__PACKAGE__");
    }

    [Fact] void should_keep_the_row_entity() => ViewsSource("ViewRow").ShouldContain("@Entity(name = \"StageViewRow\")");
    [Fact] void should_keep_the_row_table() => ViewsSource("ViewRow").ShouldContain("@Table(name = \"stage_view_row\")");
    [Fact] void should_keep_the_generation_entity() => ViewsSource("ViewGeneration").ShouldContain("@Entity(name = \"StageViewGeneration\")");
    [Fact] void should_keep_the_generation_table() => ViewsSource("ViewGeneration").ShouldContain("@Table(name = \"stage_view_generation\")");
    [Fact] void should_not_auto_register_view_entities() => PersistentSource("StageAxonPersistenceAutoConfiguration").ShouldNotContain("runtime.views");
    [Fact] void should_refuse_async_writes() => ViewsSource("JpaReadModelStore").ShouldContain("Thread.currentThread() != handlerThread");
    [Fact] void should_refuse_contextless_writes() => ViewsSource("JpaReadModelStore").ShouldContain("if (context == null) throw new IllegalStateException");
    [Fact] void should_never_create_an_independent_write_executor() => ViewsSource("JpaReadModelStore").ShouldContain("findTransactionalExecutor(context)");
    [Fact] void should_never_fall_back_to_an_executor() => ViewsSource("JpaReadModelStore").ShouldNotContain("getTransactionalExecutor(");
    [Fact] void should_serialize_row_payloads() => ViewsSource("JpaReadModelStore").ShouldContain("mapper.writeValueAsString(next)");
    [Fact] void should_guard_both_retained_events_and_checkpoints() => ViewsSource("PersistentViewBootstrap").ShouldContain("Stream.of(AggregateEventEntry.class, TokenEntry.class, ViewRow.class)");
    [Fact] void should_refuse_generation_changes() => ViewsSource("PersistentViewBootstrap").ShouldContain("!existing.fingerprint().equals(fingerprint)");
    [Fact] void should_refuse_swallowing_customizations() => ViewsSource("PersistentViewProcessorGuard").ShouldContain("processor.errorHandler() != PropagatingErrorHandler.INSTANCE");
    [Fact] void should_reject_each_missing_persistent_processor() => ViewsSource("PersistentViewProcessorGuard").ShouldContain("if (!(processors.get(name) instanceof PooledStreamingEventProcessor)) throw refusal();");
    [Fact] void should_reject_subscribing_persistent_processor_registrations() => ViewsSource("PersistentViewProcessorGuard").ShouldContain("otherProcessors.containsKey(name) && !(otherProcessors.get(name) instanceof PooledStreamingEventProcessor)");
    [Fact] void should_import_stream_in_the_bootstrap() => ViewsSource("PersistentViewBootstrap").ShouldContain("import java.util.stream.Stream;");
    [Fact] void should_use_the_short_stream_in_the_bootstrap() => ViewsSource("PersistentViewBootstrap").ShouldContain("return Stream.of(AggregateEventEntry.class");

    [Fact] void should_canonicalize_map_keys() => ViewsSource("JpaReadModelStore").ShouldContain("new TreeMap<>(map)");
    [Fact] void should_use_the_same_identity_for_removal() => ViewsSource("JpaReadModelStore").ShouldContain("identity(key)");

    static string ViewsSource(string name) => File.ReadAllText(Path.Combine(ModuleRoot(), "main", "java", "io", "cratis", "stage", "axon", "runtime", "views", name + ".java"));

    static string ModuleRoot() => Path.Combine(for_AxonArtifactRenderPlanner.given.blueprint_models.Root(), "Source", "Runtime", "src");

    static string PersistentSource(string name) => File.ReadAllText(Path.Combine(ModuleRoot(), "main", "java", "io", "cratis", "stage", "axon", "runtime", "persistent", name + ".java"));

    static string Source(string name) => File.ReadAllText(Path.Combine(for_AxonArtifactRenderPlanner.given.blueprint_models.Root(), "Source", "Runtime", "src", "main", "java", "io", "cratis", "stage", "axon", "runtime", name + ".java"));
}
