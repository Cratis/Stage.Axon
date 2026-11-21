// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Admission.for_AxonBehaviorAdmission;

public class when_the_model_uses_screenplay_632_constructs : given.a_model_with_behaviors
{
    [Theory]
    [InlineData("audiences")]
    [InlineData("availabilities")]
    [InlineData("application provides")]
    [InlineData("application screens")]
    [InlineData("module provides")]
    [InlineData("feature provides")]
    [InlineData("slice provides")]
    [InlineData("screen adoptions")]
    [InlineData("ability invocation")]
    void should_warn_by_default_and_refuse_in_strict_mode(string construct)
    {
        var provided = new SemanticProvidedValue("Tenant", new SemanticValueProvideSource(new("tenant", null)));
        var located = ScopeSelection.Select(request).First(item => !item.Slice.Commands.IsDefaultOrEmpty);
        var application = construct switch
        {
            "audiences" => request.Model.Application with { Audiences = [new("Staff")] },
            "availabilities" => request.Model.Application with { Availabilities = [new("Open", [])] },
            "application provides" => request.Model.Application with { Provides = [provided] },
            "application screens" => request.Model.Application with { Screens = [new(default, "Screen", "Title", [], [], [], new([], []))] },
            _ => request.Model.Application
        };
        located = construct switch
        {
            "module provides" => located with { Module = located.Module with { Provides = [provided] } },
            "feature provides" => located with { Features = [.. located.Features.Select(feature => feature with { Provides = [provided] })] },
            "slice provides" => located with { Slice = located.Slice with { Provides = [provided] } },
            "screen adoptions" => located with { Slice = located.Slice with { Adoptions = [new(default, [])] } },
            "ability invocation" => located with { Slice = located.Slice with { Reactions = [new(default, "Notify", [new SemanticReactionTrigger(SemanticReactionTriggerKind.Startup) { Invokes = [new SemanticInvocation(default, []) { Ability = "Send" }] }])] } },
            _ => located
        };
        var decisions = AxonUnsupportedSemantics.Decisions(application, [located], "example").Where(decision => decision.Code == AxonRefusalCodes.UnsupportedSemantics).ToArray();
        decisions.ShouldNotBeEmpty();
        context = new(request, new(AxonRendering.CreateProfile()));
        AxonBehaviorAdmission.Apply(context, decisions);
        context.Degradation.UnsupportedConstructs.All(diagnostic => diagnostic.Severity == ArtifactRenderDiagnosticSeverity.Warning).ShouldBeTrue();
        context = new(request, new(AxonRendering.CreateProfile(strict: true)));
        AxonBehaviorAdmission.Apply(context, decisions);
        context.Degradation.Refusals.SelectMany(refusal => refusal.Codes).Distinct().ShouldContainOnly("STAGE-AXON-UNSUPPORTED-SEMANTICS");
    }
}
