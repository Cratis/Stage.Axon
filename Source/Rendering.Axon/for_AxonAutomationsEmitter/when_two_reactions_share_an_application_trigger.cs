// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonAutomationsEmitter;

public class when_two_reactions_share_an_application_trigger : given.a_reaction_model
{
    const string Body = "var early = true;\nif (early) return;";
    string code;

    void Because()
    {
        var located = ScopeSelection.Select(request).First(slice => slice.Slice.Commands.Any(candidate => candidate.Id == command.Id));
        var second = new SemanticReaction(SemanticId.Create(SemanticKind.Reaction, "Audit"), "Audit", [reaction.Triggers[0] with { Invokes = [], RequirementId = "native-audit" }]) { RunsAs = new(SemanticReactionIdentityKind.System, []) };
        var owner = SemanticAddress.ForReaction(SemanticAddress.ForSlice(ApplicationIdentity.Create(request.Model.Application.Name), located.Module.Name, [.. located.Features.Select(feature => feature.Name)], located.Slice.Name), second.Name);
        var requirement = new SemanticImplementationRequirement(SemanticImplementationRole.ReactionEffect, owner, "trigger:0", "java", null, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Body))), new(second.Id, default, default))
        {
            RequirementId = "native-audit",
            RequiredCapability = "provider-defined"
        };
        request = request with { ImplementationRequirements = [requirement], ImplementationContents = request.ImplementationContents.SetItem("native-audit", Body) };
        reaction = reaction with { RunsAs = new(SemanticReactionIdentityKind.System, []) };
        otherReactions = [second];
        Render();
        code = Code("SubmitOrderEndpoint");
    }

    [Fact] void should_call_every_effect_from_the_endpoint() => code.ShouldContain("runSubmitOrder(input, metadata);\n        runAudit(input, metadata);");
    [Fact] void should_keep_each_effect_in_its_own_method() => code.ShouldContain("private void runAudit(SubmitOrder input, Metadata metadata)");
    [Fact] void should_keep_the_early_return_inside_its_own_effect() => code.ShouldContain("if (early) return;\n        }\n        finally");
    [Fact] void should_still_send_the_other_reactions_command() => code.ShouldContain("private void runSubmitOrder(SubmitOrder input, Metadata metadata)");
    [Fact] void should_inject_the_clock_for_the_native_body() => code.ShouldContain("private final Clock clock;");
}
