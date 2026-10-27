// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Admission.for_AxonBehaviorAdmission;

public class when_an_event_is_public : given.a_model_with_behaviors
{
    SemanticEventContract contract;
    AxonBehaviorDecision[] decisions;

    void Establish() => contract = ScopeSelection.Select(request).SelectMany(located => located.Slice.Events).First() with { Visibility = SemanticEventVisibility.Public };

    // A public event is only valid in a translation slice; the walk reads the slice as given.
    void Because() => decisions = [.. Public(Slice(contract))];

    [Fact] void should_report_0601_on_the_event_contract() => decisions.Single().Owner.ShouldEqual(contract.Id);
    [Fact] void should_use_the_translation_code() => decisions.Single().Code.ShouldEqual("STAGE-AXON-0601");
    [Fact] void should_describe_the_publication() => decisions.Single().Intent.ShouldContain("publish the public event contract " + contract.Name);
    [Fact] void should_not_report_a_private_event() => Public(Slice(contract with { Visibility = SemanticEventVisibility.Private })).ShouldBeEmpty();

    static IEnumerable<AxonBehaviorDecision> Public(SemanticSlice slice) => AxonBehaviorAdmission.SliceDecisions("sample.orders", slice).Where(decision => decision.ModelPath.EndsWith("/public", StringComparison.Ordinal));

    SemanticSlice Slice(SemanticEventContract published) => Seed() with { Kind = SemanticSliceKind.Translate, Events = [published] };
}
