// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Admission.for_AxonBehaviorAdmission;

public class when_the_model_uses_screenplay_6_constructs : given.a_model_with_behaviors
{
    IReadOnlyList<AxonBehaviorDecision> _decisions;

    void Establish()
    {
        var application = request.Model.Application with { StateMachines = [new("Lifecycle", SemanticId.Create(SemanticKind.ReadModel, "Lifecycle"), [], [])] };
        var located = ScopeSelection.Select(request)[0];
        var command = located.Slice.Commands[0] with { Calendar = new("UTC", "Zone") };
        _decisions = [.. AxonUnsupportedSemantics.Decisions(application, [located with { Slice = located.Slice with { Commands = [command] } }], "base")];
    }

    [Fact] void should_report_the_state_machine() => _decisions.Any(decision => decision.Intent.Contains("state machine Lifecycle", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_report_the_command_calendar() => _decisions.Any(decision => decision.Intent.Contains("command calendar", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_use_only_the_one_code() => _decisions.Select(decision => decision.Code).Distinct().ShouldContainOnly("STAGE-AXON-UNSUPPORTED-SEMANTICS");

    [Fact]
    void should_refuse_in_strict_mode()
    {
        context = new(request, new(AxonRendering.CreateProfile(strict: true)));
        AxonBehaviorAdmission.Apply(context, _decisions);
        context.Degradation.Refusals.SelectMany(refusal => refusal.Codes).Distinct().ShouldContainOnly("STAGE-AXON-UNSUPPORTED-SEMANTICS");
    }
}
