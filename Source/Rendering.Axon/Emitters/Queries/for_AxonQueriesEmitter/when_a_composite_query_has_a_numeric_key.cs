// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter.given;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter;

public class when_a_composite_query_has_a_numeric_key : a_view_request
{
    string _endpoint;

    void Because()
    {
        var context = new AxonEmissionContext(_request, new(_request.Profile));
        new AxonQueriesEmitter().Emit(context);
        _endpoint = Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/FindMetricEndpoint.java", StringComparison.Ordinal)));
    }

    [Fact] void should_use_the_short_primitive_parser() => _endpoint.ShouldContain("new FindMetric(region, Long.valueOf(month))");
    [Fact] void should_not_qualify_the_unambiguous_primitive_parser() => _endpoint.ShouldNotContain("java.lang.Long");
    [Fact] void should_not_import_java_lang() => _endpoint.ShouldNotContain("import java.lang.Long;");
}
