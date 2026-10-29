// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Screenplay.Semantics.Execution;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;

namespace Cratis.Stage.Rendering.Axon.Verification;

/// <summary>Completes the native proof model with the semantic shapes exercised by the ingress tests.</summary>
internal static class IngressProofModel
{
    internal const string Application = "ingress-invariants";

    internal static ArtifactRenderPlan Plan(LoadedSemanticModel loaded, ArtifactRenderProfile? profile = null) => new AxonArtifactRenderPlanner().Plan(Request(loaded, profile));

    static ArtifactRenderRequest Request(LoadedSemanticModel loaded, ArtifactRenderProfile? profile)
    {
        var slices = loaded.Model.Application.Modules.Single().Features.Single().Slices;
        var commands = slices.SelectMany(slice => slice.Commands).ToArray();
        var register = commands.Single(command => command.Name == "RegisterAccount");
        var registered = slices.SelectMany(slice => slice.Events).Single(contract => contract.Name == "AccountRegistered");
        var closed = slices.SelectMany(slice => slice.Events).Single(contract => contract.Name == "AccountClosed");
        var consumed = slices.SelectMany(slice => slice.Events).Single(contract => contract.Name == "AccountConsumed");
        var view = slices.SelectMany(slice => slice.ReadModels).Single();
        var actor = registered.Properties.Single(property => property.Name == "actor");
        var alias = registered.Properties.Single(property => property.Name == "alias");
        var constraint = new SemanticConstraint("AccountAlias", SemanticConstraintKind.UniquePropertyValue, SemanticConstraintScope.EventSequence, [new(registered.Id, [alias.Id])], [closed.Id], true, "Alias already claimed");

        SemanticCommand Command(SemanticCommand command)
        {
            if (command.Id == register.Id)
            {
                return command with
                {
                    Produces = [.. command.Produces.Select(produced => produced with
                    {
                        Mappings = [.. produced.Mappings.Select(mapping => mapping.TargetProperty == actor.Id
                            ? mapping with { Source = new SemanticEventContextExpression(SemanticEventContextValueKind.CausedBySubject, actor.Type) }
                            : mapping)]
                    })]
                };
            }
            var key = command.Properties.Single(property => property.Name == (command.Name == "CloseOtherAccount" ? "other" : "id"));
            return command with { Reads = [new("account", view.Id, key.Id, SemanticDecisionReadPresence.Required)] };
        }

        SemanticFeature Feature(SemanticFeature feature) => feature with
        {
            Features = [.. feature.Features.Select(Feature)],
            Slices = [.. feature.Slices.Select(slice => slice with
            {
                Commands = [.. slice.Commands.Select(Command)],
                Queries = [.. slice.Queries.Select(query => query is SemanticKeyedQuery keyed ? keyed with { Delivery = SemanticQueryDelivery.Live } : query)],
                Constraints = slice.Commands.Any(command => command.Id == register.Id) ? [constraint] : slice.Constraints,
                ReadModels = [.. slice.ReadModels.Select(model => model with { Properties = [.. model.Properties.Select(property => property with { IsIdentifier = property.Name == "id" })] })],
                Projections = [.. slice.Projections.Select(projection => projection with
                {
                    Scope = projection.Scope! with { Removals = [new(closed.Id, new SemanticProjectionValueKey(SemanticProjectionValue.EventSourceIdentity), null), new(consumed.Id, new SemanticProjectionValueKey(SemanticProjectionValue.EventSourceIdentity), null)] }
                })]
            })]
        };
        var application = loaded.Model.Application with
        {
            CommandReadMode = SemanticCommandReadMode.Protected,
            Modules = [.. loaded.Model.Application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })]
        };
        var model = ExecutableSemanticModel.Create(new(25, 0), SemanticVersion.V25, application);
        var scope = new ArtifactRenderScope(ArtifactRenderScopeKind.Application, application.Id);
        return new(model, SemanticExecutionPlan.Compile(model).Plan!, profile ?? AxonRendering.CreateProfile(), scope);
    }
}
