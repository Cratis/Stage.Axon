// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter.given;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Specifications.for_AxonHttpTests;

public class when_describing_stream_problems : a_query_collision
{
    string _stream;

    void Because()
    {
        _query = _query with { Delivery = SemanticQueryDelivery.Live };
        Render();
        AxonHttpTests.Emit(_context);
        var json = Encoding.UTF8.GetString(_context.Artifacts.Single(artifact => artifact.RelativePath == "src/main/resources/http-contract.json").Bytes.AsSpan());
        _stream = json.Split('\n').Single(line => line.Contains("find-total", StringComparison.Ordinal) && line.Contains("\"kind\":\"stream\"", StringComparison.Ordinal));
    }

    [Fact] void should_list_the_live_capacity_problem() => _stream.ShouldContain("\"problems\":[{\"type\":\"urn:cratis:stage-axon:problem:live-capacity\",\"status\":503}]");
}
