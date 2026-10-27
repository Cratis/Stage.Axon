// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Runtime.for_AxonRuntimeEmitter;

public class when_emitting_problem_details : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string _source;

    void Because()
    {
        var context = new AxonEmissionContext(request, new(request.Profile));
        new AxonRuntimeEmitter().Emit(context);
        _source = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/ProblemDetailsAdvice.java", StringComparison.Ordinal)).Bytes.AsSpan());
    }

    [Theory]
    [InlineData("validation")]
    [InlineData("conflict")]
    [InlineData("invalid-argument")]
    [InlineData("unsupported")]
    [InlineData("query-result-missing")]
    [InlineData("query-cardinality")]
    void should_use_a_stable_problem_type(string type) => _source.ShouldContain($"\"{type}\"");

    [Fact] void should_use_the_urn_prefix() => _source.ShouldContain("urn:cratis:stage-axon:problem:");
    [Fact] void should_render_framework_errors_as_problem_details() => _source.ShouldContain("extends ResponseEntityExceptionHandler");
    [Fact] void should_map_cardinality_failures() => _source.ShouldContain("@ExceptionHandler(QueryCardinalityException.class)");
    [Theory]
    [InlineData("Exception")]
    [InlineData("RuntimeException")]
    [InlineData("Throwable")]
    void should_not_swallow_security_exceptions_with_a_catch_all(string type) => _source.ShouldNotContain($"@ExceptionHandler({type}.class)");
}
