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
    void should_use_a_stable_problem_type(string type) => Source("ProblemDetailsAdvice").ShouldContain($"\"{type}\"");

    [Fact] void should_use_the_urn_prefix() => Source("ProblemDetailsAdvice").ShouldContain("urn:cratis:stage-axon:problem:");
    [Fact] void should_render_framework_errors_as_problem_details() => Source("ProblemDetailsAdvice").ShouldContain("extends ResponseEntityExceptionHandler");
    [Fact] void should_not_map_cardinality_failures() => Source("ProblemDetailsAdvice").ShouldNotContain("QueryCardinalityException");
    [Fact] void should_not_publish_query_results() => AxonRuntimePackage.ModuleTypes.Contains("QueryResults").ShouldBeFalse();
    [Fact] void should_not_publish_a_cardinality_exception() => AxonRuntimePackage.ModuleTypes.Contains("QueryCardinalityException").ShouldBeFalse();
    [Fact] void should_not_map_the_deleted_capacity_exception() => Source("ProblemDetailsAdvice").ShouldNotContain("LiveCapacityException");

    [Theory]
    [InlineData("Exception")]
    [InlineData("RuntimeException")]
    [InlineData("Throwable")]
    void should_not_swallow_security_exceptions_with_a_catch_all(string type) => Source("ProblemDetailsAdvice").ShouldNotContain($"@ExceptionHandler({type}.class)");

    [Theory]
    [InlineData("ProblemDetailsAdvice")]
    void should_declare_the_runtime_module_package(string name) => Source(name).ShouldContain("package io.cratis.stage.axon.runtime;");

    static string Source(string name) => File.ReadAllText(Path.Combine(for_AxonArtifactRenderPlanner.given.blueprint_models.Root(), "Source", "Runtime", "src", "main", "java", "io", "cratis", "stage", "axon", "runtime", name + ".java"));
}
