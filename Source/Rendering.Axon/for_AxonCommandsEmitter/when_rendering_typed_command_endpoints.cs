// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter;

public class when_rendering_typed_command_endpoints : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string scalarCode;
    string recordCode;
    string expectedScalarType;

    void Because()
    {
        var located = ScopeSelection.Select(request).First(slice => !slice.Slice.Commands.IsEmpty);
        var seed = located.Slice.Commands[0];
        var property = seed.Properties[0];
        var types = new CommandJavaTypes(request.Model.Application);
        expectedScalarType = types.Type(property.Type);
        scalarCode = Endpoint(located, seed with { Response = new SemanticScalarCommandResponse(property.Id, property.Type) });
        recordCode = Endpoint(located, seed with { Response = new SemanticRecordCommandResponse([new(Name: "orderId", Type: property.Type, Source: property.Id)]) });
    }

    [Fact] void should_return_the_scalar_concept_type() => scalarCode.ShouldContain($"CompletableFuture<{expectedScalarType}>");
    [Fact] void should_dispatch_with_the_scalar_type() => scalarCode.ShouldContain($"send(command, {expectedScalarType}.class)");
    [Fact] void should_return_the_response_record_type() => recordCode.ShouldContain("CompletableFuture<PlaceOrderResponse>");
    [Fact] void should_dispatch_with_the_response_record_type() => recordCode.ShouldContain("send(command, PlaceOrderResponse.class)");

    string Endpoint(LocatedSemanticSlice located, SemanticCommand command)
    {
        var context = new AxonEmissionContext(request, new(request.Profile));
        CommandEndpointEmitter.Emit(context, located, command, null);
        return Encoding.UTF8.GetString(context.Artifacts.Single().Bytes.AsSpan());
    }
}
