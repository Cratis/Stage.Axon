// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Events;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonEventsEmitter;

public class when_rendering_event_tag_constants : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext context;
    string tags;
    string events;

    void Because()
    {
        context = new(request, new(request.Profile));
        new AxonEventsEmitter().Emit(context);
        new EventTagsEmitter().Emit(context);
        tags = Code(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/EventTags.java", StringComparison.Ordinal)));
        events = string.Join('\n', context.Artifacts.Where(artifact => !artifact.RelativePath.EndsWith("/EventTags.java", StringComparison.Ordinal)).Select(Code));
    }

    [Fact] void should_emit_a_final_utility_class() => tags.ShouldContain("public final class EventTags");
    [Fact] void should_prevent_instantiation() => tags.ShouldContain("private EventTags() {}");
    [Fact] void should_preserve_the_original_tag_value() => tags.ShouldContain("public static final String ORDER_ID = \"orderId\";");
    [Fact] void should_deduplicate_shared_tag_keys() => (tags.Split("public static final String ORDER_ID", StringSplitOptions.None).Length - 1).ShouldEqual(1);
    [Fact] void should_reference_the_constant_from_event_components() => events.ShouldContain($"EventTag(key = {context.BasePackage}.orders.events.EventTags.ORDER_ID)");
    [Fact] void should_not_repeat_the_tag_literal_in_events() => events.ShouldNotContain("key = \"orderId\"");
    [Fact] void should_omit_redundant_json_annotations() => events.ShouldNotContain("@com.fasterxml.jackson.annotation.JsonProperty(\"customer\")");
    [Fact] void should_keep_event_metadata() => events.ShouldContain("@org.axonframework.messaging.eventhandling.annotation.Event");

    [Fact]
    void should_not_emit_constants_for_a_module_without_events()
    {
        var application = request.Model.Application;
        var empty = application.Modules[0] with { Features = [] };
        var emptyRequest = request with { Model = ExecutableSemanticModel.Create(LanguageVersion.V1, SemanticVersion.V1, application with { Modules = [empty] }) };
        var emptyContext = new AxonEmissionContext(emptyRequest, new(request.Profile));
        new EventTagsEmitter().Emit(emptyContext);
        emptyContext.Artifacts.ShouldBeEmpty();
    }

    [Fact]
    void should_emit_constants_for_each_selected_module()
    {
        foreach (var module in ScopeSelection.Select(request).Where(slice => !slice.Slice.Events.IsEmpty).Select(slice => slice.Module).DistinctBy(module => module.Id))
            context.Artifacts.Count(artifact => artifact.RelativePath == $"src/main/java/{context.BasePackage}/{JavaNames.PackageSegment(module.Name)}/events/EventTags.java").ShouldEqual(1);
    }

    static string Code(PlannedArtifact artifact) => Encoding.UTF8.GetString(artifact.Bytes.AsSpan());
}
