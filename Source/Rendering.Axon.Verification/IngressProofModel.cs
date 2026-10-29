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
        var (trigger, reaction) = TriggerFor(register, "RegisterRequested", new(SemanticReactionIdentityKind.System, ["Writer"]));
        var identities = IdentityProof(slices.Single(slice => slice.Commands.Any(command => command.Id == register.Id)), Command(register), registered);
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
                Queries = [.. slice.Queries.Select(HttpProofQuery)],
                Constraints = slice.Commands.Any(command => command.Id == register.Id) ? [constraint] : slice.Constraints,
                ReadModels = [.. slice.ReadModels.Select(model => model with { Properties = [.. model.Properties.Select(property => property with { IsIdentifier = property.Name == "id" })] })],
                Projections = [.. slice.Projections.Select(projection => projection with
                {
                    Scope = projection.Scope! with { Removals = [new(closed.Id, new SemanticProjectionValueKey(SemanticProjectionValue.EventSourceIdentity), null), new(consumed.Id, new SemanticProjectionValueKey(SemanticProjectionValue.EventSourceIdentity), null)] }
                })]
            }), .. feature.Slices.Where(slice => slice.Commands.Any(command => command.Id == register.Id)).SelectMany(slice => new[] { TriggerSlice(slice, reaction), identities.Slice })]
        };
        var application = loaded.Model.Application with
        {
            CommandReadMode = SemanticCommandReadMode.Protected,
            Triggers = [.. loaded.Model.Application.Triggers, trigger, identities.Trigger],
            Policies = [.. loaded.Model.Application.Policies, new("IsSystemActor", new SemanticRoleCondition("SYSTEM"))],
            Modules = [.. loaded.Model.Application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })]
        };
        var model = ExecutableSemanticModel.Create(new(25, 0), SemanticVersion.V25, application);
        var scope = new ArtifactRenderScope(ArtifactRenderScopeKind.Application, application.Id);
        return new(model, SemanticExecutionPlan.Compile(model).Plan!, profile ?? AxonRendering.CreateProfile(), scope);
    }

    // The source compiler admits optional and collection queries, so the exactly-one HTTP proof is completed here.
    static SemanticKeyedQuery HttpProofQuery(SemanticKeyedQuery query) => query with
    {
        Delivery = SemanticQueryDelivery.Live,
        Cardinality = query.Name == "RequiredAccountSnapshot" ? SemanticQueryCardinality.One : query.Cardinality
    };

    static SemanticSlice TriggerSlice(SemanticSlice slice, SemanticReaction reaction) => slice with
    {
        Id = SemanticId.Create(SemanticKind.Slice, "RegisterRequested"),
        Name = "RegisterRequested",
        Kind = SemanticSliceKind.Automation,
        Commands = [],
        Events = [],
        Constraints = [],
        ReadModels = [],
        Projections = [],
        Queries = [],
        Specifications = [],
        Reducers = [],
        Captures = [],
        Reactions = [reaction]
    };

    static (SemanticApplicationTrigger Trigger, SemanticReaction Reaction) TriggerFor(SemanticCommand command, string name, SemanticReactionIdentity? identity)
    {
        var trigger = new SemanticApplicationTrigger(SemanticId.Create(SemanticKind.Trigger, name), name, [.. command.Properties.Select(property => property with { Id = SemanticId.Create(SemanticKind.Property, name + "." + property.Name) })]);
        var invocation = new SemanticInvocation(command.Id, [.. command.Properties.Select(property => new SemanticPropertyMapping(property.Id, new SemanticResolvedExpression(SemanticExpressionRootKind.Trigger, SemanticExpressionSourceKind.Property, trigger.Properties.Single(input => input.Name == property.Name).Id)))]);
        var source = new SemanticReactionTrigger(SemanticReactionTriggerKind.ApplicationTrigger) { Source = trigger.Id, Invokes = [invocation] };
        var reaction = new SemanticReaction(SemanticId.Create(SemanticKind.Reaction, name), name, [source]) { RunsAs = identity };
        return (trigger, reaction);
    }

    static (SemanticSlice Slice, SemanticApplicationTrigger Trigger) IdentityProof(SemanticSlice seed, SemanticCommand register, SemanticEventContract registered)
    {
        var system = IdentityCommand(register, "RegisterSystemAccount", new SemanticPolicyReference("IsSystemActor"));
        var caller = IdentityCommand(register, "RegisterCallerAccount", null);
        var (trigger, http) = TriggerFor(caller, "CallerRegistrationRequested", null);
        var requested = registered with
        {
            Id = SemanticId.Create(SemanticKind.EventContract, "SystemRegistrationRequested"),
            ContractId = EventContractId.Parse("evt1:" + new string('1', 64)),
            Name = "SystemRegistrationRequested",
            StoredName = "SystemRegistrationRequested",
            Properties = [.. register.Properties.Select(property => property with { Id = SemanticId.Create(SemanticKind.Property, "SystemRegistrationRequested." + property.Name), Name = property.IsIdentifier ? "accountId" : property.Name })]
        };
        var invocation = new SemanticInvocation(system.Id, [.. system.Properties.Select(property => new SemanticPropertyMapping(property.Id, property.IsIdentifier
            ? new SemanticEventContextExpression(SemanticEventContextValueKind.EventSourceIdentity, property.Type)
            : new SemanticResolvedExpression(SemanticExpressionRootKind.Event, SemanticExpressionSourceKind.Property, requested.Properties.Single(input => input.Name == property.Name).Id)))]);
        var background = new SemanticReaction(SemanticId.Create(SemanticKind.Reaction, "RegisterInBackground"), "RegisterInBackground", [new(SemanticReactionTriggerKind.Event) { Source = requested.Id, Invokes = [invocation] }]) { RunsAs = new(SemanticReactionIdentityKind.System, ["SYSTEM"]) };
        return (TriggerSlice(seed, background) with { Name = "ReactionIdentity", Id = SemanticId.Create(SemanticKind.Slice, "ReactionIdentity"), Commands = [system, caller], Events = [requested], Reactions = [background, http] }, trigger);
    }

    static SemanticCommand IdentityCommand(SemanticCommand command, string name, SemanticAuthorization? authorization)
    {
        var properties = command.Properties.Select(property => property with { Id = SemanticId.Create(SemanticKind.Property, name + "." + property.Name) }).ToArray();
        SemanticExpression? Map(SemanticExpression? expression) => expression is SemanticResolvedExpression reference
            ? reference with { Target = properties.Single(property => property.Name == command.Properties.Single(original => original.Id == reference.Target).Name).Id }
            : expression;
        return command with
        {
            Id = SemanticId.Create(SemanticKind.Command, name),
            Name = name,
            Properties = [.. properties],
            Authorization = authorization,
            Destination = command.Destination is { } destination ? destination with { Value = Map(destination.Value)! } : null,
            Produces = [.. command.Produces.Select(produced => produced with { Destination = Map(produced.Destination), Mappings = [.. produced.Mappings.Select(mapping => mapping with { Source = Map(mapping.Source)! })] })]
        };
    }
}
