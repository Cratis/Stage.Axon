// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter;

public partial class when_emitting_query_types_with_short_names : given.a_query_collision
{
    [Theory]
    [InlineData("FindTotal")]
    [InlineData("FindTotalHandler")]
    [InlineData("FindTotalEndpoint")]
    [InlineData("LiveTotals")]
    [InlineData("LiveTotalsHandler")]
    [InlineData("LiveTotalsEndpoint")]
    [InlineData("LiveTotalsSubscription")]
    [InlineData("LiveTotalsStreams")]
    [InlineData("RankedTotals")]
    [InlineData("RankedTotalsHandler")]
    [InlineData("RankedTotalsEndpoint")]
    [InlineData("RankedTotalsPerformer")]
    [InlineData("RankedTotalsPerformerConfiguration")]
    void should_not_qualify_types_outside_imports(string name)
    {
        Render();
        var body = string.Join('\n', Code(name).Split('\n').Where(line => !line.StartsWith("package ", StringComparison.Ordinal) && !line.StartsWith("import ", StringComparison.Ordinal)));
        body = Strings.Replace(body, "\"\"");
        QualifiedTypes.Matches(body).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Object", "LiveTotalsStreams", "Flow.Publisher<java.lang.Object>")]
    [InlineData("Throwable", "LiveTotalsStreams", "onError(java.lang.Throwable failure)")]
    [InlineData("Long", "LiveTotalsSubscription", "java.lang.Long.MAX_VALUE")]
    [InlineData("Flow", "LiveTotalsStreams", "java.util.concurrent.Flow.Publisher<Object>")]
    [InlineData("Flow", "LiveTotalsSubscription", "java.util.concurrent.Flow.Publisher<LiveTotals.Result>")]
    [InlineData("IllegalStateException", "FindTotalHandler", "new java.lang.IllegalStateException(")]
    void should_reserve_the_model_before_framework_names(string model, string file, string expected)
    {
        _model = _model with { Name = model };
        Render();
        Code(file).ShouldContain(expected);
    }

    [GeneratedRegex("\"(?:\\\\.|[^\"\\\\])*\"", RegexOptions.None, 1000)]
    private static partial Regex Strings { get; }

    [GeneratedRegex(@"(?<![\w.])(?:java|javax|jakarta|org|io|axonviews)\.[a-z0-9_.]*\.[A-Z]\w*", RegexOptions.None, 1000)]
    private static partial Regex QualifiedTypes { get; }
}
