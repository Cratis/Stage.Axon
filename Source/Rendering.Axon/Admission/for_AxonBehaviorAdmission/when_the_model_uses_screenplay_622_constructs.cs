// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Admission.for_AxonBehaviorAdmission;

public class when_the_model_uses_screenplay_622_constructs : given.a_model_with_behaviors
{
    [Theory]
    [InlineData("screens")]
    [InlineData("systems")]
    [InlineData("agents")]
    [InlineData("set once")]
    [InlineData("children ordering")]
    [InlineData("caller identity")]
    [InlineData("text contains")]
    [InlineData("text starts with")]
    void should_warn_by_default_and_refuse_in_strict_mode(string construct)
    {
        var application = request.Model.Application;
        application = construct switch
        {
            "systems" => application with { Systems = [new(default, "External", [])] },
            "agents" => application with { Agents = new(SemanticAgentTokenKind.Personal, []) },
            _ => application
        };
        var located = ScopeSelection.Select(request).First(item => construct == "caller identity" || construct.StartsWith("text", StringComparison.Ordinal) ? !item.Slice.Commands.IsDefaultOrEmpty : !item.Slice.Projections.IsDefaultOrEmpty);
        var slice = located.Slice;
        slice = construct switch
        {
            "screens" => slice with { Screens = [new(default, "Screen", "Title", [], [], [], new([], []))] },
            "set once" => slice with { Projections = [slice.Projections[0] with { Scope = SemanticProjectionScope.Empty with { Every = new(false, false, [new([], SemanticProjectionOperation.SetOnce, null)]) } }] },
            "children ordering" => slice with { Projections = [slice.Projections[0] with { Scope = SemanticProjectionScope.Empty with { Children = [new(default, default, SemanticProjectionScope.Empty) { Ordering = new(SemanticChildrenPlacement.Prepend, [], SemanticSortDirection.Ascending) }] } }] },
            "caller identity" => slice with { Commands = [slice.Commands[0] with { Requirements = [new(new SemanticComparison(new(default, null) { CallerId = true }, SemanticComparisonOperator.Equal, new(default, new SemanticTextValue("caller"))), null)] }] },
            "text contains" => slice with { Commands = [slice.Commands[0] with { Requirements = [new(new SemanticComparison(new(default, new SemanticTextValue("haystack")), SemanticComparisonOperator.Contains, new(default, new SemanticTextValue("needle"))), null)] }] },
            "text starts with" => slice with { Commands = [slice.Commands[0] with { Requirements = [new(new SemanticComparison(new(default, new SemanticTextValue("haystack")), SemanticComparisonOperator.StartsWith, new(default, new SemanticTextValue("needle"))), null)] }] },
            _ => slice
        };
        var decisions = AxonUnsupportedSemantics.Decisions(application, [located with { Slice = slice }], "example").Where(decision => decision.Intent.Contains(construct.Split(' ')[0], StringComparison.Ordinal)).ToArray();
        decisions.ShouldNotBeEmpty();
        context = new(request, new(AxonRendering.CreateProfile()));
        AxonBehaviorAdmission.Apply(context, decisions);
        context.Degradation.UnsupportedConstructs.All(diagnostic => diagnostic.Severity == ArtifactRenderDiagnosticSeverity.Warning).ShouldBeTrue();
        context = new(request, new(AxonRendering.CreateProfile(strict: true)));
        AxonBehaviorAdmission.Apply(context, decisions);
        context.Degradation.Refusals.SelectMany(refusal => refusal.Codes).Distinct().ShouldContainOnly("STAGE-AXON-UNSUPPORTED-SEMANTICS");
    }
}
