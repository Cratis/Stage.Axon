// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Specifications.for_AxonSpecificationsEmitter;

public class when_emitting_the_specification_manifest : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext context;
    SemanticSpecification specification;

    void Establish()
    {
        context = new(request, new AxonDegradation(request.Profile));
        specification = ScopeSelection.Select(request).SelectMany(slice => slice.Slice.Specifications).First();
    }

    [Fact]
    void should_record_a_registered_executable_specification()
    {
        AxonSpecificationsEmitter.Register(context, specification.Id, "example.OrderTest", "should_place_order");
        new AxonSpecificationsEmitter().Emit(context);
        Manifest().ShouldContain(specification.Id + "\texample.OrderTest#should_place_order\n");
    }

    [Fact]
    void should_mark_unclaimed_specifications_disabled_and_explain_their_intent()
    {
        new AxonSpecificationsEmitter().Emit(context);
        Manifest().ShouldContain(specification.Id + "\tdisabled STAGE-AXON-SPEC\t");
        var tests = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/UnimplementedSpecificationsTest.java", StringComparison.Ordinal)).Bytes.AsSpan());
        tests.ShouldContain("@Disabled(\"STAGE-AXON-SPEC:");
        tests.ShouldContain("@DisplayName(\"" + specification.Id + "\")");
        tests.ShouldContain("// Screenplay wanted to verify");
    }

    [Fact]
    void should_reject_duplicate_registrations()
    {
        AxonSpecificationsEmitter.Register(context, specification.Id, "example.OrderTest", "first");
        Assert.Throws<ArgumentException>(() => AxonSpecificationsEmitter.Register(context, specification.Id, "example.OrderTest", "second"));
    }

    string Manifest() => Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath == "stage-specifications.txt").Bytes.AsSpan());
}
