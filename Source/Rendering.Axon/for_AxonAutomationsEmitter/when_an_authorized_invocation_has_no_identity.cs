// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonAutomationsEmitter;

public class when_an_authorized_invocation_has_no_identity : given.a_reaction_model
{
    void Establish() => command = command with { Authorization = new SemanticPolicyReference("IsSales") };

    void Because() => Render();

    [Fact] void should_not_register_the_reaction() => Code("SubmitOrderReaction").ShouldNotContain("@org.springframework.stereotype.Component");
    [Fact] void should_record_the_missing_identity() => context.Degradation.UnsupportedConstructs.Any(construct => construct.Code == "STAGE-AXON-0505").ShouldBeTrue();
    [Fact] void should_record_the_inactive_trigger() => context.Degradation.UnsupportedConstructs.Any(construct => construct.Code == "STAGE-AXON-0504").ShouldBeTrue();
    [Fact] void should_preserve_the_original_intent() => Code("SubmitOrderReaction").ShouldContain("// Screenplay wanted to execute reaction SubmitOrder trigger 1");
    [Fact] void should_not_expose_the_trigger_endpoint() => context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("SubmitOrderEndpoint.java", StringComparison.Ordinal)).ShouldBeFalse();
}
