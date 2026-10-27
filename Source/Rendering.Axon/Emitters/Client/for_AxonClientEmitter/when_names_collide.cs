// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Client.for_AxonClientEmitter;

public class when_names_collide : given.a_client_request
{
    string module;

    void Because()
    {
        // The descriptor of this query is `decodeOrders`, which the Orders read model already uses for its decoder.
        Render(application => ChangeSlices(application, slice => slice with
        {
            Queries = [.. slice.Queries.Select(query => query.Name == "AllOrders" ? query with { Name = "DecodeOrders" } : query)]
        }));
        module = Text("client/src/orders/orders/orderlist.ts");
    }

    [Fact] void should_render() => plan.Success.ShouldBeTrue();
    [Fact] void should_keep_the_first_declaration() => module.ShouldContain("export function decodeOrders(value: unknown, path: string): Orders");
    [Fact] void should_suffix_the_second_declaration() => module.ShouldContain("export const decodeOrders2: QueryDescriptor<");
}
