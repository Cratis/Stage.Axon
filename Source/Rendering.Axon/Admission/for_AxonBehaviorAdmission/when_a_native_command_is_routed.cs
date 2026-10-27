// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Admission.for_AxonBehaviorAdmission;

public class when_a_native_command_is_routed : given.a_model_with_behaviors
{
    const string Routing = "STAGE-AXON-ROUTING";

    SemanticCommand command;

    void Establish()
    {
        command = ScopeSelection.Select(request).SelectMany(located => located.Slice.Commands).First() with { Handler = new("handler", 1), Route = new(SemanticId.Create(SemanticKind.EventSource, "Orders"), SemanticId.Create(SemanticKind.EventStream, "Archive")) };
    }

    [Fact]
    void should_report_the_routing_on_the_command() =>
        Decisions().Single().Owner.ShouldEqual(command.Id);

    [Fact]
    void should_report_a_warning_and_comment_by_default()
    {
        Apply(AxonRendering.CreateProfile());
        context.Degradation.UnsupportedConstructs.Single(diagnostic => diagnostic.Code == Routing).Severity.ShouldEqual(ArtifactRenderDiagnosticSeverity.Warning);
        PackageInfo().Single(text => text.Contains(Routing, StringComparison.Ordinal)).ShouldContain("route events through modeled source and substream partitions");
    }

    [Fact]
    void should_refuse_in_strict_mode()
    {
        Apply(AxonRendering.CreateProfile(strict: true));
        context.Degradation.Refusals.Any(refusal => refusal.Codes.Contains(Routing)).ShouldBeTrue();
    }

    [Fact]
    void should_not_report_an_appending_declarative_command() =>
        AxonBehaviorAdmission.SliceDecisions("sample.orders", Seed() with { Commands = [command with { Handler = null }] }).Where(decision => decision.Code == Routing).ShouldBeEmpty();

    IReadOnlyList<AxonBehaviorDecision> Decisions() => [.. AxonBehaviorAdmission.SliceDecisions("sample.orders", Seed() with { Commands = [command with { Produces = [] }] }).Where(decision => decision.Code == Routing)];

    void Apply(ArtifactRenderProfile profile)
    {
        request = request with { Profile = profile };
        context = new(request, new(request.Profile));
        AxonBehaviorAdmission.Apply(context, Decisions());
    }
}
