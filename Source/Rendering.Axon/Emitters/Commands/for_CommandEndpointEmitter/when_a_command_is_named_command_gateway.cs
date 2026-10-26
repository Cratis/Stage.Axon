// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands.for_CommandEndpointEmitter;

public class when_a_command_is_named_command_gateway : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string code;

    void Because()
    {
        var slice = ScopeSelection.Select(request).First(candidate => !candidate.Slice.Commands.IsEmpty);
        var command = slice.Slice.Commands[0] with { Name = "CommandGateway" };
        var context = new AxonEmissionContext(request, new(request.Profile));
        CommandEndpointEmitter.Emit(context, slice, command, null);
        code = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/CommandGatewayEndpoint.java", StringComparison.Ordinal)).Bytes.AsSpan());
    }

    [Fact] void should_keep_the_command_parameter_short() => code.ShouldContain("execute(@RequestBody CommandGateway request)");
    [Fact] void should_qualify_the_colliding_axon_gateway() => code.ShouldContain("private final org.axonframework.messaging.commandhandling.gateway.CommandGateway commandGateway;");
    [Fact] void should_not_import_the_colliding_axon_gateway() => code.ShouldNotContain("import org.axonframework.messaging.commandhandling.gateway.CommandGateway;");
    [Fact] void should_import_future_responses() => code.ShouldContain("import java.util.concurrent.CompletableFuture;");
}
