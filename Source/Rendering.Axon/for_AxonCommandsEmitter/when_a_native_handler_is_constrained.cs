// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Emitters.Constraints;
using Cratis.Stage.Rendering.Axon.Emitters.Implementations;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter;

public abstract class when_a_native_handler_is_constrained : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    protected string requirementId;
    protected SemanticCommand command;
    private protected LocatedSemanticSlice located;
    protected SemanticConstraint constraint;
    protected string body;

    protected virtual bool Releases => false;
    protected virtual SemanticConstraintKind Kind => SemanticConstraintKind.UniquePropertyValue;
    protected virtual string CreateBody(string append) => "/* native-constraint-body */\nappender.append(" + append + ");";

    protected virtual SemanticConstraint CreateConstraint(SemanticEventContract contract, SemanticEventContract producedContract) =>
        new("CustomerUnique", Kind, SemanticConstraintScope.EventSequence, [new(contract.Id, Kind == SemanticConstraintKind.UniquePropertyValue ? [contract.Properties.Single(property => property.Name == "customer").Id] : [])], Releases ? [producedContract.Id] : [], false, null);

    protected static string Text(PlannedArtifact artifact) => Encoding.UTF8.GetString(artifact.Bytes.AsSpan());

    void Establish()
    {
        var slices = ScopeSelection.Select(request).ToArray();
        var contract = slices.SelectMany(slice => slice.Slice.Events).Single(candidate => candidate.Name == "OrderPlaced");
        var producedContract = Releases ? slices.SelectMany(slice => slice.Slice.Events).Single(candidate => candidate.Name == "OrderMoved") : contract;
        command = slices.SelectMany(slice => slice.Slice.Commands).First(candidate => candidate.Produces.Any(produced => produced.EventContract == producedContract.Id));
        requirementId = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{command.Id}|CommandHandler|0:")));
        command = command with { Handler = new(requirementId, 1) };
        constraint = CreateConstraint(contract, producedContract);
        SemanticFeature Feature(SemanticFeature feature) => feature with
        {
            Features = [.. feature.Features.Select(Feature)],
            Slices = [.. feature.Slices.Select(slice => slice with
            {
                Commands = [.. slice.Commands.Select(candidate => candidate.Id == command.Id ? command : candidate)],
                Constraints = slice.Events.Any(candidate => candidate.Id == contract.Id) ? [constraint] : slice.Constraints
            })]
        };
        var application = request.Model.Application with { Modules = [.. request.Model.Application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })] };
        request = request with { Model = ExecutableSemanticModel.Create(new(23, 0), SemanticVersion.V23, application) };
        located = ScopeSelection.Select(request).Single(slice => slice.Slice.Commands.Any(candidate => candidate.Id == command.Id));
        var context = new AxonEmissionContext(request, new(request.Profile));
        var source = CommandSource.Create(context, command, producedContract);
        var append = new CommandAppends(context, command, source).Build(command.Produces[0]);
        Assert.NotNull(append);
        body = CreateBody(append.Value.Event);
        var sliceAddress = SemanticAddress.ForSlice(ApplicationIdentity.Create(application.Name), located.Module.Name, [.. located.Features.Select(feature => feature.Name)], located.Slice.Name);
        var requirement = new SemanticImplementationRequirement(SemanticImplementationRole.CommandHandler, SemanticAddress.ForCommand(sliceAddress, command.Name), null, "java", null, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body))), new(command.Id, default, default))
        {
            RequirementId = requirementId,
            RequiredCapability = "provider-defined"
        };
        request = request with { ImplementationRequirements = [requirement], ImplementationContents = request.ImplementationContents.SetItem(requirementId, body) };
    }

    private protected (AxonEmissionContext Context, string Code) Emit(bool strict)
    {
        var scoped = request with { Profile = AxonRendering.CreateProfile(strict: strict) };
        var context = new AxonEmissionContext(scoped, new(scoped.Profile));
        var source = CommandSource.Create(context, command, CommandAppends.Contract(context, command.Produces[0].EventContract));
        new CommandHandlerEmitter(context, located, command, source).Emit();
        InlineImplementationDiagnostics.Emit(context);
        return (context, Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + command.Name + "Handler.java", StringComparison.Ordinal))));
    }

    protected void AssertEnforced(bool strict)
    {
        var (context, code) = Emit(strict);
        ConstraintCoverage.Supported(context, constraint).ShouldBeTrue();
        code.ShouldNotContain(AxonRefusalCodes.Constraint);
        code.ShouldNotContain("UnsupportedOperationException");
        context.Degradation.UnsupportedConstructs.Any(item => item.Code == AxonRefusalCodes.Constraint).ShouldBeFalse();
        context.ConsumedImplementations.ShouldContain(requirementId);
        code.ShouldContain("ModeledConstraints.enforcing(uniqueValues, unconstrainedAppender, context)");
        code.ShouldContain("EventAppender unconstrainedAppender");
        code.ShouldContain("private void handleBody(" + command.Name + " command, EventAppender appender");
        code.ShouldNotContain(", Nullable ");
        var delegation = code.IndexOf("handleBody(command, ModeledConstraints.enforcing", StringComparison.Ordinal);
        (code.IndexOf("rules.validate(command);", StringComparison.Ordinal) < delegation).ShouldBeTrue();
        var privateMethod = code.IndexOf("private void handleBody(", StringComparison.Ordinal);
        (privateMethod > delegation).ShouldBeTrue();
        code[..privateMethod].ShouldNotContain("native-constraint-body");
        code[privateMethod..].ShouldContain("native-constraint-body");
        code.Split("native-constraint-body", StringSplitOptions.None).Length.ShouldEqual(2);
        context.Degradation.Refusals.ShouldBeEmpty();
    }

    protected void AssertRefused(bool strict, string intent)
    {
        var (context, code) = Emit(strict);
        code.ShouldContain(AxonRefusalCodes.Constraint);
        code.ShouldContain("// Screenplay wanted to");
        code.ShouldContain(intent);
        code.ShouldContain("throw new UnsupportedOperationException(");
        code.ShouldNotContain("native-constraint-body");
        code.ShouldNotContain("rules.validate(command);");
        code.ShouldNotContain("loadManagedEntity(");
        context.ConsumedImplementations.ShouldBeEmpty();
        var diagnostic = context.Degradation.UnsupportedConstructs.Single(candidate => candidate.Code == AxonRefusalCodes.Constraint && candidate.Artifact == command.Id);
        diagnostic.Severity.ShouldEqual(strict ? ArtifactRenderDiagnosticSeverity.Error : ArtifactRenderDiagnosticSeverity.Warning);
        context.Degradation.Refusals.IsEmpty.ShouldEqual(!strict);
        Text(context.Artifacts[^1]).ShouldContain(requirementId);
        Text(context.Artifacts[^1]).ShouldContain(AxonRefusalCodes.Implementation);
        UnsupportedSummary.Emit(context);
        Text(context.Artifacts.Single(artifact => artifact.RelativePath == "UNSUPPORTED.md")).ShouldContain(AxonRefusalCodes.Constraint);
    }
}
