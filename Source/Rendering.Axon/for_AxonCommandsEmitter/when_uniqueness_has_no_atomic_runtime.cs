// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Emitters.Constraints;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter;

public class when_uniqueness_has_no_atomic_runtime : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext context;
    string handler;
    SemanticCommand command;
    SemanticCommand unrelated;

    void Because()
    {
        var slices = ScopeSelection.Select(request).ToArray();
        command = slices.SelectMany(slice => slice.Slice.Commands).Single(command => command.Name == "PlaceOrder");
        unrelated = slices.SelectMany(slice => slice.Slice.Commands).Single(command => command.Name == "MoveOrder");
        var contract = slices.SelectMany(slice => slice.Slice.Events).Single(contract => contract.Id == command.Produces[0].EventContract);
        var constraint = new SemanticConstraint("CustomerUnique", SemanticConstraintKind.UniqueEventOccurrence, SemanticConstraintScope.EventSequence, [new(contract.Id, [])], [], false, null);
        SemanticFeature Feature(SemanticFeature feature) => feature with { Features = [.. feature.Features.Select(Feature)], Slices = [.. feature.Slices.Select(slice => slice.Commands.Any(candidate => candidate.Id == command.Id) ? slice with { Constraints = [constraint] } : slice)] };
        var app = loaded.Model.Application with { Modules = [.. loaded.Model.Application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })] };
        var model = ExecutableSemanticModel.Create(loaded.Model.LanguageVersion, loaded.Model.SemanticVersion, app);
        request = new(model, loaded.Plan, request.Profile, new(ArtifactRenderScopeKind.Application, app.Id));
        context = new(request, new(request.Profile));
        new AxonCommandsEmitter().Emit(context);
        handler = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("PlaceOrderHandler.java", StringComparison.Ordinal)).Bytes.AsSpan());
    }

    [Fact] void should_refuse_the_unprotected_append() => handler.ShouldContain("throw new UnsupportedOperationException(\"STAGE-AXON-0801");
    [Fact] void should_not_append_any_facts() => handler.ShouldNotContain("appender.append(");
    [Fact] void should_keep_the_required_atomic_intent() => handler.ShouldContain("enforce or release modeled uniqueness in the append unit of work");
    [Fact] void should_report_the_unimplemented_guarantee() => context.Degradation.UnsupportedConstructs.Any(diagnostic => diagnostic.Code == "STAGE-AXON-0801" && diagnostic.Artifact == command.Id).ShouldBeTrue();
    [Fact] void should_disable_dependent_native_specifications() => context.Specifications.Values.Any(specification => specification.DisabledCode == "STAGE-AXON-0112").ShouldBeTrue();
    [Fact] void should_detect_the_constrained_command() => ConstraintCoverage.CommandRequiresUnsupportedConstraint(context, command).ShouldBeTrue();
    [Fact] void should_not_block_unrelated_commands() => ConstraintCoverage.CommandRequiresUnsupportedConstraint(context, unrelated).ShouldBeFalse();
}
