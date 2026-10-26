// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Events.for_AxonEventsEmitter;

public class when_an_event_is_named_event : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string code;

    void Because()
    {
        static SemanticFeature Feature(SemanticFeature feature) => feature with
        {
            Features = [.. feature.Features.Select(Feature)],
            Slices = [.. feature.Slices.Select(slice => slice with { Events = [.. slice.Events.Select(contract => contract.Name == "OrderPlaced" ? contract with { Name = "Event" } : contract)] })]
        };
        var application = request.Model.Application;
        var model = ExecutableSemanticModel.Create(request.Model.LanguageVersion, request.Model.SemanticVersion, application with
        {
            Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })]
        });
        var context = new AxonEmissionContext(request with { Model = model }, new(request.Profile));
        new AxonEventsEmitter().Emit(context);
        code = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/Event.java", StringComparison.Ordinal)).Bytes.AsSpan());
    }

    [Fact] void should_keep_the_generated_event_name_short() => code.ShouldContain("public record Event(");
    [Fact] void should_qualify_the_colliding_framework_annotation() => code.ShouldContain("@org.axonframework.messaging.eventhandling.annotation.Event(namespace = \"orders\", name = \"Event\", version = \"1\")");
    [Fact] void should_not_import_the_colliding_framework_annotation() => code.ShouldNotContain("import org.axonframework.messaging.eventhandling.annotation.Event;");
    [Fact] void should_still_import_the_primary_tag_annotation() => code.ShouldContain("import org.axonframework.eventsourcing.annotation.EventTag;");
}
