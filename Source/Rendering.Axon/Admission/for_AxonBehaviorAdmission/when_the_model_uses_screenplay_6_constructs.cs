// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
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

    static IReadOnlyList<AxonBehaviorDecision> Decisions(SemanticApplication application, LocatedSemanticSlice? located = null) =>
        [.. AxonUnsupportedSemantics.Decisions(application, located is null ? [] : [located], "base")];

    LocatedSemanticSlice Slice(Func<SemanticSlice, SemanticSlice> change)
    {
        var first = ScopeSelection.Select(request)[0];
        return first with { Slice = change(first.Slice) };
    }

    static SemanticRequirement DateOnlyRequirement()
    {
        var property = SemanticId.Create(SemanticKind.Property, "When");
        return new(new SemanticComparison(new(property, null) { DateOf = true }, SemanticComparisonOperator.Equal, new(property, null)), "Same day");
    }

    [Fact] void should_report_the_state_machine() => _decisions.Any(decision => decision.Intent.Contains("state machine Lifecycle", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_report_the_command_calendar() => _decisions.Any(decision => decision.Intent.Contains("command calendar", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_use_only_the_one_code() => _decisions.Select(decision => decision.Code).Distinct().ShouldContainOnly("STAGE-AXON-UNSUPPORTED-SEMANTICS");

    [Fact] void should_report_a_date_only_condition_operand() => Decisions(request.Model.Application, Slice(slice => slice with { Commands = [slice.Commands[0] with { Requirements = [DateOnlyRequirement()] }] })).Any(decision => decision.Intent.Contains("date-only operand", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact]
    void should_warn_by_default_and_refuse_in_strict_mode_for_a_date_only_operand()
    {
        var decisions = Decisions(request.Model.Application, Slice(slice => slice with { Commands = [slice.Commands[0] with { Requirements = [DateOnlyRequirement()] }] }));
        context = new(request, new(AxonRendering.CreateProfile()));
        AxonBehaviorAdmission.Apply(context, decisions);
        context.Degradation.UnsupportedConstructs.All(diagnostic => diagnostic.Severity == ArtifactRenderDiagnosticSeverity.Warning).ShouldBeTrue();
        context = new(request, new(AxonRendering.CreateProfile(strict: true)));
        AxonBehaviorAdmission.Apply(context, decisions);
        context.Degradation.Refusals.SelectMany(refusal => refusal.Codes).Distinct().ShouldContainOnly("STAGE-AXON-UNSUPPORTED-SEMANTICS");
    }

    [Fact] void should_report_the_application_time_zone() => Decisions(request.Model.Application with { TimeZone = new("Europe/Oslo", false) }).Any(decision => decision.Intent.Contains("application time zone Europe/Oslo", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_report_the_application_context_members() => Decisions(request.Model.Application with { ContextMembers = ["Tenant"] }).Any(decision => decision.Intent.Contains("application context members", StringComparison.Ordinal)).ShouldBeTrue();

    [Fact]
    void should_warn_in_default_mode_and_refuse_in_strict_mode_for_the_application_time_zone()
    {
        var decisions = Decisions(request.Model.Application with { TimeZone = new("Europe/Oslo", false), ContextMembers = ["Tenant"] });
        context = new(request, new(AxonRendering.CreateProfile()));
        AxonBehaviorAdmission.Apply(context, decisions);
        context.Degradation.UnsupportedConstructs.All(diagnostic => diagnostic.Severity == ArtifactRenderDiagnosticSeverity.Warning).ShouldBeTrue();
        context = new(request, new(AxonRendering.CreateProfile(strict: true)));
        AxonBehaviorAdmission.Apply(context, decisions);
        context.Degradation.Refusals.SelectMany(refusal => refusal.Codes).Distinct().ShouldContainOnly("STAGE-AXON-UNSUPPORTED-SEMANTICS");
    }

    [Fact]
    void should_refuse_in_strict_mode()
    {
        context = new(request, new(AxonRendering.CreateProfile(strict: true)));
        AxonBehaviorAdmission.Apply(context, _decisions);
        context.Degradation.Refusals.SelectMany(refusal => refusal.Codes).Distinct().ShouldContainOnly("STAGE-AXON-UNSUPPORTED-SEMANTICS");
    }
}
