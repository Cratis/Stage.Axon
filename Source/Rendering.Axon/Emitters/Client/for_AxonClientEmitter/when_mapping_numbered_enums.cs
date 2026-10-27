// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Client.for_AxonClientEmitter;

public class when_mapping_numbered_enums : given.a_client_request
{
    string concepts;

    static SemanticConcept Number(SemanticConcept concept) => concept.Name switch
    {
        "OrderStatus" => concept with { Numbers = [10, 20, 30, 40, 50] },
        _ => concept
    };

    void Because()
    {
        Render(application => application with
        {
            Concepts = [.. application.Concepts.Select(Number)]
        });
        concepts = Text("client/src/concepts.ts");
    }

    [Fact] void should_use_a_numeric_union() => concepts.ShouldContain("export type OrderStatus = 10 | 20 | 30 | 40 | 50;");
    [Fact] void should_check_membership() => concepts.ShouldContain("const orderStatusValues = [10, 20, 30, 40, 50] as const;");
}
