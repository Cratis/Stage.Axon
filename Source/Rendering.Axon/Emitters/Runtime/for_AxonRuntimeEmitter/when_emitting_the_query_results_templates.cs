// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Runtime.for_AxonRuntimeEmitter;

public class when_emitting_the_query_results_templates : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext _context;

    void Because()
    {
        _context = new AxonEmissionContext(request, new(request.Profile));
        new AxonRuntimeEmitter().Emit(_context);
    }

    [Fact] void should_emit_the_results_helper_into_the_runtime_package() => Source("QueryResults").ShouldContain($"package {_context.BasePackage}.runtime;");
    [Fact] void should_require_exactly_one() => Source("QueryResults").ShouldContain("public static <T> List<T> exactlyOne(List<T> rows)");
    [Fact] void should_allow_at_most_one() => Source("QueryResults").ShouldContain("public static <T> List<T> atMostOne(List<T> rows)");
    [Fact] void should_emit_the_cardinality_exception_into_the_runtime_package() => Source("QueryCardinalityException").ShouldContain($"package {_context.BasePackage}.runtime;");
    [Fact] void should_not_put_row_data_in_the_exception_message() => Source("QueryCardinalityException").ShouldContain("super(\"Expected \" + cardinality + \" query result\");");
    [Fact] void should_not_leave_the_runtime_placeholder() => Source("QueryResults").ShouldNotContain("__RUNTIME__");

    string Source(string name) => Encoding.UTF8.GetString(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)).Bytes.AsSpan());
}
