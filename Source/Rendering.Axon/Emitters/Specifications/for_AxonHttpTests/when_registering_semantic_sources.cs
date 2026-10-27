// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Specifications.for_AxonHttpTests;

public class when_registering_semantic_sources : Specification
{
    string plain;
    string sourced;

    void Because()
    {
        plain = Json(new("query", "GET", "/api/a", null, null, [new("id", true)], "one"));
        sourced = Json(new("query", "GET", "/api/a", null, null, [new("id", true, new(SemanticTypeReferenceKind.Primitive, SemanticPrimitiveType.Uuid, default, false, false))], "one", default(SemanticId)));
    }

    static string Json(HttpEndpoint endpoint) => JsonSerializer.Serialize(AxonHttpContractJson.Describe(endpoint));

    [Fact] void should_not_change_the_contract_file() => sourced.ShouldEqual(plain);
}
