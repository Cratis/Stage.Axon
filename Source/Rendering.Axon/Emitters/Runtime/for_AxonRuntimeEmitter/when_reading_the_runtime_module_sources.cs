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
    void should_declare_the_runtime_module_package(string name) => Source(name).ShouldContain("package io.cratis.stage.axon.runtime;");

    [Theory]
    [InlineData("QueryResults")]
    [InlineData("LiveSnapshots")]
    [InlineData("LiveStreams")]
    [InlineData("LiveCapacityException")]
    [InlineData("SseBridge")]
    void should_not_carry_a_license_header(string name) => Source(name).ShouldNotContain("Copyright");

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

    static string Source(string name) => File.ReadAllText(Path.Combine(for_AxonArtifactRenderPlanner.given.blueprint_models.Root(), "Source", "Runtime", "src", "main", "java", "io", "cratis", "stage", "axon", "runtime", name + ".java"));
}
