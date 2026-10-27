// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter.given;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries.for_QueryEndpoint;

public class when_deriving_query_routes : a_query_collision
{
    string _route;
    string _underscored;

    void Because()
    {
        var located = ScopeSelection.Select(_request).Single(slice => slice.Slice.Queries.Any(query => query.Name == "FindTotal"));
        _route = QueryEndpoint.Route(located, _query);
        _underscored = QueryEndpoint.Route(located, _query with { Name = "Find_Total" });
    }

    [Fact] void should_use_module_features_and_kebab_query_name() => _route.ShouldEqual("/api/views/counters/find-total");
    [Fact] void should_map_underscores_to_hyphens() => _underscored.ShouldEqual("/api/views/counters/find-total");
}
