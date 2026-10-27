// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonRendering.when_planning_through_the_facade;

public class without_options : given.a_facade_model
{
    ArtifactRenderPlan _actual;
    ArtifactRenderPlan _expected;

    void Establish() => _expected = AxonRendering.PlanFrom(loaded, scope);
    void Because() => _actual = AxonRendering.Plan(Request(new()));

    [Fact] void should_keep_artifact_bytes_and_order() => EqualArtifacts(_actual, _expected);
    [Fact] void should_keep_diagnostics() => EqualDiagnostics(_actual, _expected);
    [Fact] void should_keep_the_digest() => _actual.Digest.ShouldEqual(_expected.Digest);
    [Fact] void should_not_add_names() => AxonRendering.CreateProfile().Inputs.Any(input => input.Name == AxonNamesInput.Name).ShouldBeFalse();
}
