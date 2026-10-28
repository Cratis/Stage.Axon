// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter.given;
using Cratis.Stage.Rendering.Axon.Emitters.Queries;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_emitting_a_view_without_a_projection_with_a_query_performer : a_view_request
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void should_keep_the_persistent_guard_for_performer_writes(bool sliceScoped, bool strict)
    {
        var model = ReadModelId("Total");
        SemanticFeature Feature(SemanticFeature feature) => feature with
        {
            Features = [.. feature.Features.Select(Feature)],
            Slices = [.. feature.Slices.Select(slice => slice with { Projections = [.. slice.Projections.Where(projection => projection.ReadModel != model)] })]
        };
        var application = _request.Model.Application;
        application = application with { Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })] };
        var owner = ScopeSelection.Locate(application).Single(located => located.Slice.ReadModels.Any(candidate => candidate.Id == model));
        var request = _request with
        {
            Model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application),
            Profile = AxonRendering.CreateProfile(strict: strict),
            Scope = sliceScoped ? new(ArtifactRenderScopeKind.Slice, owner.Slice.Id) : _request.Scope
        };
        var context = new AxonEmissionContext(request, new(request.Profile));
        new AxonProjectionsEmitter().Emit(context);
        new AxonQueriesEmitter().Emit(context);

        var configuration = Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/TotalStoreConfiguration.java", StringComparison.Ordinal)));
        configuration.ShouldContain("return new InMemoryReadModelStore<>();");
        configuration.ShouldContain("persistentReadModelSafety()");
        configuration.ShouldContain("@Profile(\"persistent\")");
        configuration.ShouldContain("STAGE-AXON-0309");
        configuration.ShouldContain("only query performers can change it, and their writes are volatile");
        configuration.ShouldContain("Persistent startup refuses this read model because");
        configuration.ShouldNotContain("always reads empty");
        var diagnostic = context.Degradation.UnsupportedConstructs.Single(diagnostic => diagnostic.Artifact == model);
        diagnostic.Code.ShouldEqual("STAGE-AXON-0309");
        diagnostic.Severity.ShouldEqual(strict ? ArtifactRenderDiagnosticSeverity.Error : ArtifactRenderDiagnosticSeverity.Warning);
        Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/RankedTotalsPerformer.java", StringComparison.Ordinal))).ShouldContain("ReadModelStore<Object, Total> store");
    }
}
