// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonRendering.when_planning_through_the_facade;

public class with_a_normalized_java_package : given.a_facade_model
{
    [Theory]
    [InlineData("Acme.Orders", "acme.orders")]
    [InlineData("acme.orders_v2", "acme.ordersv2")]
    [InlineData("acme.class", "acme.class_")]
    void should_use_simple_application_types_in_the_command_fixture(string package, string normalized)
    {
        var plan = AxonRendering.Plan(Request(new() { JavaPackage = package }));
        plan.Success.ShouldBeTrue();
        var path = "src/test/java/" + normalized.Replace('.', '/') + "/orders/orders/placeorder/PlaceOrderSpecificationsTest.java";
        var code = Text(plan.Artifacts.Single(artifact => artifact.RelativePath == path));
        code.ShouldContain($"package {normalized}.orders.orders.placeorder;");
        code.ShouldContain($"import {normalized}.concepts.OrderId;");
        code.ShouldContain(".command(new PlaceOrder(new OrderId(");
        code.ShouldContain("result.events(new OrderPlaced(new OrderId(");
        code.Split('\n').Where(line => !line.StartsWith("package ", StringComparison.Ordinal) && !line.StartsWith("import ", StringComparison.Ordinal))
            .All(line => !line.Contains(normalized + ".", StringComparison.Ordinal)).ShouldBeTrue();
    }
}
