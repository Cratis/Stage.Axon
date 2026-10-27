// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Automations.for_ReactionInvocation;

public class when_rendering_a_command_invocation : for_AxonAutomationsEmitter.given.a_reaction_model
{
    string _code;
    string _commandType;

    void Because()
    {
        Render();
        var located = ScopeSelection.Locate(request.Model.Application).Single(slice => slice.Slice.Commands.Any(candidate => candidate.Id == command.Id));
        _commandType = CommandContractsEmitter.Package(new(request.Model.Application), located) + "." + JavaNames.Type(command.Name);
        _code = Code("SubmitOrderReaction");
    }

    [Fact] void should_import_the_invoked_model_type() => _code.ShouldContain("import " + _commandType + ";");
    [Fact] void should_construct_the_command_by_its_short_name() => _code.ShouldContain("commands.sendAndWait(new " + JavaNames.Type(command.Name) + "(");
    [Fact] void should_not_construct_a_qualified_model_type() => _code.ShouldNotContain("new " + _commandType + "(");
    [Fact] void should_import_the_gateway() => _code.ShouldContain("import org.axonframework.messaging.commandhandling.gateway.CommandGateway;");
    [Fact] void should_use_the_short_gateway_name() => _code.ShouldContain("private final CommandGateway commands;");
}
