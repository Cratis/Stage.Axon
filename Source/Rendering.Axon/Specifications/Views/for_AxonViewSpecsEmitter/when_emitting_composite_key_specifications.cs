// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views.for_AxonViewSpecsEmitter;

public class when_emitting_composite_key_specifications : Emitters.Projections.for_AxonProjectionsEmitter.given.a_view_request
{
    string _specifications;

    void Because()
    {
        var context = new AxonEmissionContext(_request, new(_request.Profile));
        new AxonProjectionsEmitter().Emit(context);
        new AxonViewSpecsEmitter().Emit(context);
        _specifications = Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/MetricViewSpecificationsTest.java", StringComparison.Ordinal)));
    }

    [Fact] void should_enable_composite_key_expectations() => _specifications.ShouldNotContain("@Disabled");
    [Fact] void should_construct_typed_expected_keys() => _specifications.ShouldContain("repository.findById(new MetricKey(\"west\", 1L))");
    [Fact] void should_check_bulk_removal_without_reading_the_persistence_context() => _specifications.ShouldContain("!repository.existsById(new MetricKey(\"west\", 2L))");
    [Fact] void should_execute_the_long_key_round_trip() => _specifications.ShouldContain("void keepingALongCompositeKey()");
}
