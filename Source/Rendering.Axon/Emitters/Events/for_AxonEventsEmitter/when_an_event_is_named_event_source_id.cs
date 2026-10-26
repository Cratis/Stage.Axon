// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Events.for_AxonEventsEmitter;

public class when_an_event_is_named_event_source_id : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string _code;

    void Because()
    {
        var target = Sdk.ScopeSelection.Select(request).SelectMany(slice => slice.Slice.Events).Single(contract => contract.Name == "OrderPlaced").Id;
        SemanticFeature Feature(SemanticFeature feature) => feature with
        {
            Features = [.. feature.Features.Select(Feature)],
            Slices = [.. feature.Slices.Select(slice => slice with { Commands = slice.Events.Any(contract => contract.Id == target) ? [] : slice.Commands, Specifications = [], Events = [.. slice.Events.Select(contract => contract.Name == "OrderPlaced" ? contract with { Name = "EventSourceId" } : contract)] })]
        };
        var application = request.Model.Application;
        var model = ExecutableSemanticModel.Create(request.Model.LanguageVersion, request.Model.SemanticVersion, application with { Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })] });
        var context = new AxonEmissionContext(request with { Model = model }, new(request.Profile));
        new AxonEventsEmitter().Emit(context);
        _code = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/EventSourceId.java", StringComparison.Ordinal)).Bytes.AsSpan());
    }

    [Fact] void should_keep_the_application_event_name() => _code.ShouldContain("public record EventSourceId(");
    [Fact] void should_qualify_the_runtime_identity_component() => _code.ShouldContain("sample.runtime.EventSourceId eventSourceId");
    [Fact] void should_not_import_the_runtime_identity_over_the_event() => _code.ShouldNotContain("import sample.runtime.EventSourceId;");
}
