// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Specifications.for_AxonHttpTests.when_emitting_round_trips;

public class without_any_exchanges : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext context;

    void Establish()
    {
        context = new(request, new AxonDegradation(request.Profile));
        var id = ScopeSelection.Select(request).SelectMany(slice => slice.Slice.Specifications).First().Id;
        AxonHttpTests.Register(context, new(id, [], new("", [], false)));
    }

    void Because() => new AxonSpecificationsEmitter().Emit(context);

    [Fact] void should_not_emit_an_empty_passing_http_test() => context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("/ModeledHttpRoundTripTest.java", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_claim_the_specification_as_executed() => context.Specifications.Values.All(entry => entry.DisabledCode is not null).ShouldBeTrue();
    [Fact] void should_report_the_missing_http_expectations() => context.Degradation.UnsupportedConstructs.Any(diagnostic => diagnostic.Code == "STAGE-AXON-HTTP-SPEC").ShouldBeTrue();
    [Fact] void should_keep_unclaimed_specifications_explicitly_disabled() => Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath == "stage-specifications.txt").Bytes.AsSpan()).ShouldContain("disabled STAGE-AXON-SPEC");
    [Fact] void should_leave_the_http_registration_empty() => context.HttpScenarios.ShouldBeEmpty();
}
