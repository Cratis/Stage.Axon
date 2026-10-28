// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence.for_AxonPersistenceEmitter;

public class when_emitting_a_removal_proof_with_a_colliding_policy_name : for_PersistentViewAdmission.given.a_flat_view
{
    string _projector;
    string _proof;

    void Because()
    {
        var context = Render(slice => slice with
        {
            ReadModels = [.. slice.ReadModels.Select(model => model.Name == "Metric" ? model with { Name = "MetricRemovedSequence" } : model)]
        });
        _projector = Code(context, "MetricProjectionProjector");
        _proof = Code(context, "MetricRemovedSequencePersistentRemovalTest");
    }

    [Fact] void should_allocate_the_suffixed_policy_in_the_projector() => _projector.ShouldContain("public static final class MetricRemovedSequence2 ");
    [Fact] void should_reference_the_allocated_removal_policy() => _proof.ShouldContain("MetricProjectionProjector.MetricRemovedSequence2.class");
    [Fact] void should_not_reference_the_colliding_name_as_a_policy() => _proof.ShouldNotContain("MetricProjectionProjector.MetricRemovedSequence.class");
}
