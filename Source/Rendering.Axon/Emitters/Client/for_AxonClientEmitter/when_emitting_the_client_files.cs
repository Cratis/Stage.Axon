// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Client.for_AxonClientEmitter;

public class when_emitting_the_client_files : given.a_client_request
{
    void Because() => Render();

    [Fact] void should_render() => plan.Success.ShouldBeTrue();
    [Fact] void should_write_the_tsconfig() => ClientPaths.ShouldContain("client/tsconfig.json");

    [Fact]
    void should_write_the_shared_modules() =>
        new[] { "transport", "http-transport", "sse", "problems", "concepts", "http-routes" }.All(name => ClientPaths.Contains("client/src/" + name + ".ts")).ShouldBeTrue();

    [Fact] void should_write_one_module_per_slice_with_endpoints() => ClientPaths.ShouldContain("client/src/orders/orders/placeorder.ts");
    [Fact] void should_type_a_snapshot_query() => Text("client/src/orders/orders/orderlist.ts").ShouldContain("QueryDescriptor<");
    [Fact] void should_type_a_command_without_a_result() => Text("client/src/orders/fulfilment/moveorder.ts").ShouldContain("CommandDescriptor<MoveOrder, void>");
    [Fact] void should_not_write_a_license_header() => ClientPaths.Where(path => path.EndsWith(".ts", StringComparison.Ordinal)).All(path => !Text(path).Contains("Licensed under", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_not_use_fully_qualified_names() => Text("client/src/orders/orders/placeorder.ts").ShouldNotContain("import(");
}
