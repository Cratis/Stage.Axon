// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Automations.for_ReactionInvocation;

public class when_the_invoked_command_is_named_command_gateway : for_AxonAutomationsEmitter.given.a_reaction_model
{
    string _code;

    void Establish() => command = command with { Name = "CommandGateway" };

    void Because()
    {
        Render();
        _code = Code("SubmitOrderReaction");
    }

    [Fact] void should_keep_the_model_command_short() => _code.ShouldContain("commands.sendAndWait(new CommandGateway(");
    [Fact] void should_qualify_the_colliding_gateway() => _code.ShouldContain("private final org.axonframework.messaging.commandhandling.gateway.CommandGateway commands;");
    [Fact] void should_not_import_the_colliding_gateway() => _code.ShouldNotContain("import org.axonframework.messaging.commandhandling.gateway.CommandGateway;");
}
