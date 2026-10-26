// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands.for_CommandContractsEmitter;

public class when_a_command_is_named_command : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string collision;
    string regular;

    void Because()
    {
        var slice = ScopeSelection.Select(request).First(candidate => !candidate.Slice.Commands.IsEmpty);
        var command = slice.Slice.Commands[0];
        var context = new AxonEmissionContext(request, new(request.Profile));
        CommandContractsEmitter.EmitCommand(context, slice, command with { Name = "Command" }, null);
        CommandContractsEmitter.EmitCommand(context, slice, command with { Name = "StandardCommand" }, null);
        collision = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/Command.java", StringComparison.Ordinal)).Bytes.AsSpan());
        regular = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/StandardCommand.java", StringComparison.Ordinal)).Bytes.AsSpan());
    }

    [Fact] void should_keep_the_application_command_short() => collision.ShouldContain("public record Command(");
    [Fact] void should_qualify_the_colliding_axon_annotation() => collision.ShouldContain("@org.axonframework.messaging.commandhandling.annotation.Command(name = \"Command\")");
    [Fact] void should_not_import_the_colliding_axon_annotation() => collision.ShouldNotContain("import org.axonframework.messaging.commandhandling.annotation.Command;");
    [Fact] void should_import_the_annotation_when_it_does_not_collide() => regular.ShouldContain("import org.axonframework.messaging.commandhandling.annotation.Command;");

    [Fact]
    void should_not_inline_framework_types_in_a_noncolliding_record_header()
    {
        var header = string.Join('\n', regular.Split('{')[0].Split('\n').Where(line => !line.TrimStart().StartsWith("import ", StringComparison.Ordinal)));
        new[] { "java.", "org.axonframework.", "org.springframework.", "jakarta.", "io.cratis." }
            .Any(prefix => header.Contains(prefix, StringComparison.Ordinal)).ShouldBeFalse();
    }
}
