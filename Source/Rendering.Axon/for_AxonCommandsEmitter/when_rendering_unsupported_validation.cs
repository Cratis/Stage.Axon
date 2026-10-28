// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter;

public class when_rendering_unsupported_validation : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext context;
    string rules;
    SemanticCommand command;

    void Establish()
    {
        var scope = ScopeSelection.Select(request);
        var located = scope.First(slice => !slice.Slice.Commands.IsEmpty);
        var seed = located.Slice.Commands[0];
        var property = seed.Properties[0];
        command = seed with { Validations = [new(property.Id, SemanticValidationRuleKind.CodeValidation, null, null)] };
        context = new(request, new(request.Profile));
    }

    void Because()
    {
        var builder = new Java.JavaCodeBuilder("sample.commands");
        new CommandDomainChecks(context, command, new(request.Model.Application, Java.JavaNames.PackageSegment(request.Model.Application.Name))).Emit(builder);
        rules = builder.Build();
    }

    [Fact] void should_warn_instead_of_breaking_the_render() => context.Degradation.UnsupportedConstructs.Single().Severity.ShouldEqual(ArtifactRenderDiagnosticSeverity.Warning);
    [Fact] void should_use_a_registered_code() => context.Degradation.UnsupportedConstructs.Single().Code.ShouldEqual(AxonRefusalCodes.ImplementationValidation);
    [Fact] void should_preserve_the_intent_in_generated_source() => rules.ShouldContain("// Screenplay wanted to validate");
    [Fact] void should_explain_the_target_limitation() => rules.ShouldContain("but this is not supported by the Axon target yet (STAGE-AXON-IMPL-VALIDATION).");
    [Fact] void should_accept_values_without_rejecting_the_unimplemented_rule() => rules.ShouldNotContain("throw new UnsupportedOperationException");
}
