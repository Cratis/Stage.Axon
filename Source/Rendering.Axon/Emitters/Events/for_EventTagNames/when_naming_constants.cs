// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Events.for_EventTagNames;

public class when_naming_constants
{
    [Theory]
    [InlineData("orderId", "ORDER_ID")]
    [InlineData("customerID", "CUSTOMER_ID")]
    [InlineData("XMLDocumentId", "XML_DOCUMENT_ID")]
    [InlineData("order-id", "ORDER_ID")]
    [InlineData("1stSource", "_1ST_SOURCE")]
    [InlineData("_", "TAG")]
    public void should_create_a_java_constant_name(string key, string expected) => EventTagNames.Constants([key])[key].ShouldEqual(expected);

    [Fact]
    public void should_disambiguate_collisions_deterministically()
    {
        string[] keys = ["orderId", "order-id", "order_id", "orderId_2", "orderId"];
        var constants = EventTagNames.Constants(keys);
        constants.Count.ShouldEqual(4);
        constants.Values.Distinct(StringComparer.Ordinal).Count().ShouldEqual(4);
        var reversed = EventTagNames.Constants(keys.Reverse());
        foreach (var (key, name) in constants) reversed[key].ShouldEqual(name);
        constants["order-id"].ShouldEqual("ORDER_ID");
        constants["orderId"].ShouldEqual("ORDER_ID_2");
    }
}
