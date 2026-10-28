// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_a_projection_targets_another_slice : given.a_view_request
{
    AxonEmissionContext _context;
    SemanticProjection _projection;

    void Because()
    {
        var owner = ScopeSelection.Select(_request).First(located => !located.Slice.Projections.IsEmpty);
        _projection = owner.Slice.Projections[0];
        var target = owner.Slice.ReadModels.Single(model => model.Id == _projection.ReadModel);
        var destination = ScopeSelection.Select(_request).First(located => located.Slice.Id != owner.Slice.Id).Slice.Id;
        SemanticSlice Slice(SemanticSlice slice)
        {
            if (slice.Id == owner.Slice.Id) return slice with { ReadModels = [.. slice.ReadModels.Where(model => model.Id != target.Id)] };
            if (slice.Id == destination) return slice with { ReadModels = [.. slice.ReadModels, target] };
            return slice;
        }
        SemanticFeature Feature(SemanticFeature feature) => feature with
        {
            Features = [.. feature.Features.Select(Feature)],
            Slices = [.. feature.Slices.Select(Slice)]
        };
        var application = _request.Model.Application;
        var model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application with
        {
            Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })]
        });
        var request = _request with { Model = model };
        _context = new(request, new(request.Profile));
        new AxonProjectionsEmitter().Emit(_context);
    }

    [Fact]
    void should_warn_instead_of_silently_skipping_the_projection()
    {
        var diagnostic = _context.Degradation.UnsupportedConstructs.Single(diagnostic => diagnostic.Artifact == _projection.Id);
        diagnostic.Code.ShouldEqual("STAGE-AXON-0307");
        diagnostic.Severity.ShouldEqual(ArtifactRenderDiagnosticSeverity.Warning);
        diagnostic.Message.ShouldEqual("read model outside slice");
    }

    [Fact]
    void should_emit_a_stub_with_the_unsupported_intent()
    {
        var stub = _context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith('/' + JavaNames.Type(_projection.Name) + "Projector.java", StringComparison.Ordinal));
        Text(stub).ShouldContain("Differs from Screenplay: read model outside slice");
        Text(stub).ShouldContain("STAGE-AXON-0307");
        Text(stub).ShouldContain("public class");
    }
}
