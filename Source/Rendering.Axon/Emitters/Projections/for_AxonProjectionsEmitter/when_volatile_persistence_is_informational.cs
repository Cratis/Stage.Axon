// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_volatile_persistence_is_informational : given.a_view_request
{
    [Fact]
    void should_remain_a_warning_in_strict_mode()
    {
        var request = _request with { Profile = AxonRendering.CreateProfile(strict: true) };
        var context = new AxonEmissionContext(request, new(request.Profile));
        new AxonProjectionsEmitter().Emit(context);
        var model = ReadModelId("Total");
        context.Degradation.Warnings.Single(diagnostic => diagnostic.Artifact == model).Severity.ShouldEqual(ArtifactRenderDiagnosticSeverity.Warning);
        context.Degradation.UnsupportedConstructs.Any(diagnostic => diagnostic.Artifact == model).ShouldBeFalse();
    }
}
