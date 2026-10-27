// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands.for_CommandContractsEmitter;

public class when_a_command_member_name_is_a_java_keyword : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string code;

    void Because()
    {
        var slice = ScopeSelection.Select(request).First(candidate => !candidate.Slice.Commands.IsEmpty);
        var command = slice.Slice.Commands[0];
        var context = new AxonEmissionContext(request, new(request.Profile));
        code = CommandContractsEmitter.Code(context, command with { Properties = [command.Properties[0] with { Name = "class" }] }, "example.commands", null);
    }

    [Fact] void should_escape_the_java_member() => code.ShouldContain(" class_");

    // Request bodies are keyed by the Java member name on both sides (ClientSlices), so no @JsonProperty is emitted. Moving to modelled wire names is tracked in Cratis/Stage.Axon#44.
    [Fact] void should_use_the_java_member_name_on_the_wire() => code.ShouldNotContain("JsonProperty");
}
