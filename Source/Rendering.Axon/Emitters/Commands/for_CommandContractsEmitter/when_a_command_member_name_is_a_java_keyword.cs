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
        code = CommandContractsEmitter.Code(context, command with { Properties = [command.Properties[0] with { Name = "class" }, command.Properties[0] with { Name = "unchanged" }] }, "example.commands", null);
    }

    [Fact] void should_escape_the_java_member() => code.ShouldContain(" class_");

    [Fact] void should_preserve_the_modelled_wire_name() => code.ShouldContain("@JsonProperty(\"class\") ");
    [Fact] void should_import_the_annotation() => code.ShouldContain("import com.fasterxml.jackson.annotation.JsonProperty;");
    [Fact] void should_not_annotate_an_unrenamed_member() => code.ShouldNotContain("@JsonProperty(\"unchanged\")");
}
