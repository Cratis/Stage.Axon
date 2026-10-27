// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Emitters.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views.for_NativeQuerySpecifications;

public class when_exact_query_results_are_not_supported : for_NativeViewSpecifications.given.a_native_view_request
{
    async Task Establish() => _request = await LoadCanonicalCorpus();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void should_report_the_unclaimed_specification_code_not_the_view_fixture_code(bool strict)
    {
        _spec = ScopeSelection.Select(_request).SelectMany(slice => slice.Slice.Specifications).First(specification => !specification.ThenQueries.IsEmpty);
        var exact = _spec with { ThenQueries = [_spec.ThenQueries[0] with { Exactly = true }] };
        var application = _request.Model.Application;
        SemanticFeature Replace(SemanticFeature feature) => feature with
        {
            Features = [.. feature.Features.Select(Replace)],
            Slices = [.. feature.Slices.Select(slice => slice with
            {
                Specifications = [.. slice.Specifications.Select(specification => specification.Id == exact.Id ? exact : specification)]
            })]
        };
        _request = _request with
        {
            Profile = AxonRendering.CreateProfile(strict: strict),
            Model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application with
            {
                Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(Replace)] })]
            })
        };
        _context = new(_request, new(_request.Profile));
        new AxonViewSpecsEmitter().Emit(_context);
        Assert.False(_context.Specifications.ContainsKey(exact.Id));
        new AxonSpecificationsEmitter().Emit(_context);
        var diagnostic = Assert.Single(_context.Degradation.UnsupportedConstructs, diagnostic => diagnostic.Artifact == exact.Id);
        Assert.Equal("STAGE-AXON-SPEC", diagnostic.Code);
        Assert.Equal(strict ? ArtifactRenderDiagnosticSeverity.Error : ArtifactRenderDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal("STAGE-AXON-SPEC", _context.Specifications[exact.Id].DisabledCode);
        var java = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/UnimplementedSpecificationsTest.java", StringComparison.Ordinal)));
        Assert.Contains("@Disabled(\"STAGE-AXON-SPEC:", java, StringComparison.Ordinal);
        Assert.Contains("not supported by the Axon target yet (STAGE-AXON-SPEC).", java, StringComparison.Ordinal);
        Assert.DoesNotContain(_context.Degradation.UnsupportedConstructs, diagnostic => diagnostic.Artifact == exact.Id && diagnostic.Code == "STAGE-AXON-0501");
    }
}
