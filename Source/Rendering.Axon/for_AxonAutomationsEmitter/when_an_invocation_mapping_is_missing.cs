// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Automations;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonAutomationsEmitter;

public class when_an_invocation_mapping_is_missing : given.a_reaction_model
{
    string? invocation;

    void Because()
    {
        context = new(request, new(request.Profile));
        invocation = ReactionInvocation.Render(context, reaction.Triggers[0].Invokes[0] with { Mappings = [] }, new("SubmitOrder", applicationTrigger.Properties, null));
    }

    [Fact] void should_not_generate_a_partial_invocation() => invocation.ShouldBeNull();
}
