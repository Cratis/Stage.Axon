// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Admission.for_AxonBehaviorAdmission;

public class when_an_invocation_declares_refusal_branches : given.a_model_with_behaviors
{
    SemanticReaction reaction;
    AxonBehaviorDecision[] decisions;

    void Establish()
    {
        var invocation = new SemanticInvocation(SemanticId.Create(SemanticKind.Command, "Place"), []) { OnRefused = [new("validation", null, []), new("any", null, [])] };
        reaction = new(SemanticId.Create(SemanticKind.Reaction, "Retry"), "Retry", [new(SemanticReactionTriggerKind.Startup) { Invokes = [invocation, invocation with { OnRefused = [] }] }]);
    }

    void Because() => decisions = [.. AxonBehaviorAdmission.SliceDecisions("sample.orders", Seed() with { Kind = SemanticSliceKind.Automation, Reactions = [reaction] })];

    [Fact] void should_report_one_0506_per_branch_on_the_reaction() => decisions.Count(decision => decision.Code == "STAGE-AXON-0506" && decision.Owner == reaction.Id).ShouldEqual(2);
    [Fact] void should_use_a_member_precise_model_path() => decisions.Select(decision => decision.ModelPath).ShouldContainOnly(reaction.Id + "/triggers/0/invokes/0/on-refused/0", reaction.Id + "/triggers/0/invokes/0/on-refused/1");
    [Fact] void should_not_report_an_invocation_without_branches() => decisions.Any(decision => decision.ModelPath.Contains("/invokes/1/", StringComparison.Ordinal)).ShouldBeFalse();
}
