// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Events.for_AxonEventsEmitter;

public class when_an_event_is_named_its_identifier_type : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string _code;

    void Because()
    {
        static SemanticFeature Feature(SemanticFeature feature) => feature with
        {
            Features = [.. feature.Features.Select(Feature)],
            Slices = [.. feature.Slices.Select(slice => slice with { Events = [.. slice.Events.Select(contract => contract.Name == "OrderPlaced" ? contract with { Name = "OrderId" } : contract)] })]
        };
        var application = request.Model.Application;
        var model = ExecutableSemanticModel.Create(request.Model.LanguageVersion, request.Model.SemanticVersion, application with { Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })] });
        var context = new AxonEmissionContext(request with { Model = model }, new(request.Profile));
        new AxonEventsEmitter().Emit(context);
        _code = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/OrderId.java", StringComparison.Ordinal)).Bytes.AsSpan());
    }

    [Fact] void should_keep_the_application_event_name() => _code.ShouldContain("public record OrderId(");
    [Fact] void should_qualify_the_concept_identity_component() => _code.ShouldContain("sample.concepts.OrderId orderId");
    [Fact] void should_not_import_the_concept_over_the_event() => _code.ShouldNotContain("import sample.concepts.OrderId;");
}
