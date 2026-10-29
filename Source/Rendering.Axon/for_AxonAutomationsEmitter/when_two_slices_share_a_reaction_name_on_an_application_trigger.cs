// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonAutomationsEmitter;

public class when_two_slices_share_a_reaction_name_on_an_application_trigger : given.a_reaction_model
{
    string code;

    void Because()
    {
        otherSliceReactions = [reaction with { Id = SemanticId.Create(SemanticKind.Reaction, "Resubmit.SubmitOrder") }];
        Render();
        code = Code("SubmitOrderEndpoint");
    }

    [Fact] void should_qualify_the_first_runner_with_its_slice() => code.ShouldContain("private void runSubmitOrderSubmitOrder(SubmitOrder input, Metadata metadata)");
    [Fact] void should_qualify_the_second_runner_with_its_slice() => code.ShouldContain("private void runResubmitSubmitOrder(SubmitOrder input, Metadata metadata)");
    [Fact] void should_call_both_runners_from_the_endpoint() => code.ShouldContain("runSubmitOrderSubmitOrder(input, metadata);\n        runResubmitSubmitOrder(input, metadata);");
}
