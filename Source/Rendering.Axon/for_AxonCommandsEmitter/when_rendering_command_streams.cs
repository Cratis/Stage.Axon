// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter;

public class when_rendering_command_streams : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string[] streams;

    void Because()
    {
        var context = new AxonEmissionContext(request, new(request.Profile));
        new AxonCommandsEmitter().Emit(context);
        streams = [.. context.Artifacts.Where(artifact => artifact.RelativePath.EndsWith("Stream.java", StringComparison.Ordinal)).Select(artifact => Encoding.UTF8.GetString(artifact.Bytes.AsSpan()))];
    }

    [Fact] void should_use_short_entity_annotations() => streams.ShouldEachConformTo(stream => stream.Contains("@EventSourcedEntity(tagKey = ", StringComparison.Ordinal));
    [Fact] void should_import_entity_metadata() => streams.ShouldEachConformTo(stream => stream.Contains("import org.axonframework.eventsourcing.annotation.EventSourcedEntity;", StringComparison.Ordinal));

    [Fact]
    void should_not_inline_framework_types()
    {
        streams.ShouldNotBeEmpty();
        streams.ShouldEachConformTo(stream => !stream.Split('\n').Any(line => !line.TrimStart().StartsWith("import ", StringComparison.Ordinal) &&
            new[] { "java.", "org.axonframework.", "org.springframework.", "jakarta.", "io.cratis." }.Any(prefix => line.Contains(prefix, StringComparison.Ordinal))));
    }
}
