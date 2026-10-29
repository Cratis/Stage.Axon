// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands.for_CommandRulesEmitter;

public class when_a_validation_has_no_java_implementation : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    const string Reason = "The validation rule has no Java implementation; the generated validator accepts every value.";
    AxonEmissionContext context;
    string rules;
    string rejection;
    string success;
    string denial;
    string summary;

    void Because()
    {
        var located = ScopeSelection.Select(request).First(slice => !slice.Slice.Commands.IsEmpty);
        var seed = located.Slice.Commands[0];
        var command = seed with { Validations = [new(seed.Properties[0].Id, SemanticValidationRuleKind.CodeValidation, null, "Invalid")] };
        context = new(request, new(request.Profile));
        var builder = new Java.JavaCodeBuilder("example.commands");
        new CommandDomainChecks(context, command, new(request.Model.Application, context.BasePackage)).Emit(builder);
        rules = builder.Build();
        var specification = located.Slice.Specifications.First(specification => specification.When?.Command == command.Id);
        rejection = Render(located, command, specification with { ThenErrors = [new(null, "Invalid")], ThenEvents = [] });
        success = Render(located, command, specification);
        denial = Render(located, command, specification with { ThenDenied = true, ThenEvents = [] });
        UnsupportedSummary.Emit(context);
        summary = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath == "UNSUPPORTED.md").Bytes.AsSpan());
    }

    [Fact] void should_leave_the_unimplemented_rule_as_a_no_op() => rules.ShouldNotContain("throw new UnsupportedOperationException");
    [Fact] void should_explain_the_difference() => rules.ShouldContain("// Differs from Screenplay: " + Reason);
    [Fact] void should_warn() => context.Degradation.UnsupportedConstructs[0].Severity.ShouldEqual(ArtifactRenderDiagnosticSeverity.Warning);
    [Fact] void should_use_the_validation_code() => context.Degradation.UnsupportedConstructs[0].Code.ShouldEqual("STAGE-AXON-IMPL-VALIDATION");
    [Fact] void should_document_the_no_op() => summary.ShouldContain("| STAGE-AXON-IMPL-VALIDATION | ");
    [Fact] void should_document_the_reason() => summary.ShouldContain("| " + Reason + " |");
    [Fact] void should_disable_the_dependent_test() => rejection.ShouldContain("@Disabled(\"" + Reason + "\")");
    [Fact] void should_keep_the_success_test_enabled() => success.ShouldNotContain("@Disabled");
    [Fact] void should_keep_authorization_denial_enabled() => denial.ShouldNotContain("@Disabled");
    [Fact] void should_still_assert_authorization_denial() => denial.ShouldContain("result.exception(UnauthorizedMessageException.class).noEvents();");

    string Render(LocatedSemanticSlice located, SemanticCommand command, SemanticSpecification specification)
    {
        context.Specifications.Clear();
        context.Artifacts.Clear();
        var slice = located with { Slice = located.Slice with { Specifications = [specification] } };
        var source = CommandSource.Create(context, command, CommandAppends.Contract(context, command.Produces[0].EventContract));
        new CommandSpecificationsEmitter(context, slice, command, source, false).Emit();
        return Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("SpecificationsTest.java", StringComparison.Ordinal)).Bytes.AsSpan());
    }
}
