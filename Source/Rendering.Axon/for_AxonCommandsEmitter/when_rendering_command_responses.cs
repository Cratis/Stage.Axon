// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter;

public class when_rendering_command_responses : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    CommandResponses scalar;
    CommandResponses record;
    string recordCode;
    string assertionCode;
    string expectedScalarType;

    void Because()
    {
        var slice = ScopeSelection.Select(request).First(slice => !slice.Slice.Commands.IsEmpty).Slice;
        var seed = slice.Commands[0];
        var property = seed.Properties[0];
        var types = new CommandJavaTypes(request.Model.Application);
        var context = new AxonEmissionContext(request, new(request.Profile));
        var scalarCommand = seed with { Response = new SemanticScalarCommandResponse(property.Id, property.Type) };
        scalar = new(scalarCommand, types);
        record = new(seed with { Response = new SemanticRecordCommandResponse([new(Name: "orderId", Type: property.Type, Source: property.Id)]) }, types);
        record.Emit(context, "example.orders");
        recordCode = Encoding.UTF8.GetString(context.Artifacts.Single().Bytes.AsSpan());
        expectedScalarType = types.Type(property.Type);
        var specification = slice.Specifications[0] with { ThenReturns = new SemanticScalarSpecificationResponse(SemanticValue.Text("11111111-1111-1111-1111-111111111111")), ThenEvents = [] };
        var code = new JavaCodeBuilder("example.orders");
        new CommandFixtureAssertions(context, scalarCommand, specification).Emit(code, []);
        assertionCode = code.Build();
    }

    [Fact] void should_preserve_the_scalar_concept_type() => scalar.ReturnType.ShouldEqual(expectedScalarType);
    [Fact] void should_return_the_command_value() => scalar.Value.ShouldEqual("command.orderId()");
    [Fact] void should_construct_the_typed_response_record() => record.Value.ShouldEqual("new PlaceOrderResponse(command.orderId())");
    [Fact] void should_name_the_response_after_the_command() => record.ReturnType.ShouldEqual("PlaceOrderResponse");
    [Fact] void should_preserve_response_wire_names() => recordCode.ShouldContain("@com.fasterxml.jackson.annotation.JsonProperty(\"orderId\")");
    [Fact] void should_assert_the_scalar_response() => assertionCode.ShouldContain("result.resultMessagePayload(new ");
    [Fact] void should_construct_the_expected_identity() => assertionCode.ShouldContain("UUID.fromString(\"11111111-1111-1111-1111-111111111111\")");
}
