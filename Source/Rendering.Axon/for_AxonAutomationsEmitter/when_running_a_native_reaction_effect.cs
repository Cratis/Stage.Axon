// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Automations;
using Cratis.Stage.Rendering.Axon.Emitters.Implementations;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonAutomationsEmitter;

public class when_running_a_native_reaction_effect : given.a_reaction_model
{
    const string Body = "// java.time must stay authored\nvar message = \"\\\\ literal\";\nreturn;";
    string code;
    string requirementId;

    void Because()
    {
        var located = ScopeSelection.Select(request).First(slice => slice.Slice.Commands.Any(candidate => candidate.Id == command.Id));
        var owner = SemanticAddress.ForReaction(SemanticAddress.ForSlice(ApplicationIdentity.Create(request.Model.Application.Name), located.Module.Name, [.. located.Features.Select(feature => feature.Name)], located.Slice.Name), reaction.Name);
        requirementId = "native-reaction-effect";
        var requirement = new SemanticImplementationRequirement(SemanticImplementationRole.ReactionEffect, owner, "trigger:0", "java", null, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Body))), new(reaction.Id, default, default))
        {
            RequirementId = requirementId,
            RequiredCapability = "provider-defined"
        };
        request = request with { ImplementationRequirements = [requirement], ImplementationContents = request.ImplementationContents.SetItem(requirementId, Body) };
        reaction = reaction with
        {
            RunsAs = new(SemanticReactionIdentityKind.System, []),
            Triggers = [reaction.Triggers[0] with { Invokes = [], RequirementId = requirementId }]
        };
        Render();
        code = Code("SubmitOrderReaction");
    }

    [Fact] void should_copy_the_body_verbatim() => code.ShouldContain(Body);
    [Fact] void should_not_dispatch_a_modeled_command() => code.ShouldNotContain("commands.sendAndWait(");
    [Fact] void should_restore_the_previous_identity() => code.ShouldContain("finally {");
    [Fact] void should_keep_raw_body_inside_the_identity_wrapper() => Assert.True(code.IndexOf("setContext(identity);", StringComparison.Ordinal) < code.IndexOf(Body, StringComparison.Ordinal));
    [Fact] void should_mark_the_body_consumed() => context.ConsumedImplementations.ShouldContain(requirementId);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void should_refuse_an_unsupported_identity_without_consuming_the_body(bool strict)
    {
        request = request with { Profile = AxonRendering.CreateProfile(strict: strict) };
        reaction = reaction with { RunsAs = new((SemanticReactionIdentityKind)999, []) };
        context = new(request, new(request.Profile));
        Assert.Null(new ReactionTriggerEmitter(context, reaction).Prepare(reaction.Triggers[0], 0));
        InlineImplementationDiagnostics.Emit(context);
        foreach (var artifact in context.Artifacts) Encoding.UTF8.GetString(artifact.Bytes.AsSpan()).ShouldNotContain(Body);
        context.ConsumedImplementations.ShouldBeEmpty();
        context.Degradation.UnsupportedConstructs.Any(diagnostic => diagnostic.Artifact == reaction.Id).ShouldBeTrue();
        context.Degradation.Refusals.IsEmpty.ShouldEqual(!strict);
    }
}
