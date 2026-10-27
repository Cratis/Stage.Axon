// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Client.for_AxonClientEmitter;

public class when_a_command_returns_a_value : given.a_client_request
{
    string scalar;
    string record;

    string Placed(SemanticCommandResponse response)
    {
        Render(application => ChangeSlices(application, slice => slice with
        {
            Commands = [.. slice.Commands.Select(command => command.Name == "PlaceOrder" ? command with { Response = response } : command)]
        }));
        return Text("client/src/orders/orders/placeorder.ts");
    }

    void Because()
    {
        var property = loaded.Model.Application.Modules.SelectMany(module => module.Features).SelectMany(feature => feature.Slices).SelectMany(slice => slice.Commands).First(command => command.Name == "PlaceOrder").Properties[0];
        scalar = Placed(new SemanticScalarCommandResponse(property.Id, property.Type));
        record = Placed(new SemanticRecordCommandResponse([new(Name: "orderId", Type: property.Type, Source: property.Id)]));
    }

    [Fact] void should_type_the_scalar_as_the_concept() => scalar.ShouldContain("CommandDescriptor<PlaceOrder, OrderId>");
    [Fact] void should_decode_the_scalar() => scalar.ShouldContain("decode: decodeOrderId,");
    [Fact] void should_declare_the_record_response() => record.ShouldContain("export interface PlaceOrderResponse {");
    [Fact] void should_type_the_record_response() => record.ShouldContain("CommandDescriptor<PlaceOrder, PlaceOrderResponse>");
    [Fact] void should_decode_the_record_response() => record.ShouldContain("decode: decodePlaceOrderResponse,");
}
