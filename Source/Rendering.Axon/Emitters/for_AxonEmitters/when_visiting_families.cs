// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Admission;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.for_AxonEmitters;

public class when_visiting_families : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    [Fact]
    void should_keep_the_shared_family_order() =>
        AxonEmitters.Ordered.Select(emitter => emitter.GetType().Name).ShouldEqual(
            "AxonScaffoldEmitter",
            "AxonConceptsEmitter",
            "AxonEventsEmitter",
            "AxonCommandsEmitter",
            "AxonConstraintsEmitter",
            "AxonDecisionsEmitter",
            "AxonProjectionsEmitter",
            "AxonQueriesEmitter",
            "AxonAutomationsEmitter",
            "AxonPersistenceEmitter",
            "AxonSecurityEmitter",
            "AxonSpecificationsEmitter",
            "AxonRuntimeEmitter");

    [Fact]
    void should_not_invoke_unimplemented_families()
    {
        var context = new AxonEmissionContext(request, new AxonDegradation(request.Profile));
        foreach (var emitter in AxonEmitters.Ordered)
        {
            emitter.IsImplemented.ShouldBeFalse();
            Assert.Throws<NotSupportedException>(() => emitter.Emit(context));
        }
    }
}
