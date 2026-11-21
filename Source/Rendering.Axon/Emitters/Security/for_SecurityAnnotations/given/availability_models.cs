// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Cratis.Screenplay.Semantics;
using Cratis.Screenplay.Semantics.Execution;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security.for_SecurityAnnotations.given;

internal static class availability_models
{
    static readonly ImmutableArray<SemanticAvailabilityRequirement> _gate = [new SemanticAudienceRequirement("Staff")];

    internal static async Task<ArtifactRenderRequest> Request(string sample, string? owner = null, string? sliceName = null)
    {
        var root = Path.Combine(for_AxonArtifactRenderPlanner.given.blueprint_models.Root(), "Source/Rendering.Axon.Verification/Corpus");
        var compilation = await SemanticModelLoader.LoadAsync(root, [sample + ".play"], null, "Availability");
        var loaded = compilation.Loaded!;
        var application = sample == "reaction-constraints" ? WithInvocations(loaded.Model.Application) : loaded.Model.Application;
        SemanticFeature Feature(SemanticFeature feature) => feature with
        {
            Available = owner == "feature" ? _gate : feature.Available,
            Features = [.. feature.Features.Select(Feature)],
            Slices = [.. feature.Slices.Select(slice => slice with { Available = GateSlice(slice) ? _gate : slice.Available })]
        };
        bool GateSlice(SemanticSlice slice) => owner == "slice" && (sliceName is null || slice.Name == sliceName);
        application = application with
        {
            Audiences = [new("Staff")],
            Screens = [new(SemanticId.Create(SemanticKind.Screen, "Home"), "Home", "Home", [], [], [], new([], []))],
            Modules = [.. application.Modules.Select(module => module with
            {
                Available = owner == "module" ? _gate : module.Available,
                Features = [.. module.Features.Select(Feature)]
            })]
        };
        var model = ExecutableSemanticModel.Create(LanguageVersion.V56, SemanticVersion.V56, application);
        return new(model, SemanticExecutionPlan.Compile(model).Plan!, AxonRendering.WithBearerTokens(AxonRendering.CreateProfile()), new(ArtifactRenderScopeKind.Application, application.Id))
        {
            ImplementationRequirements = loaded.ImplementationRequirements,
            ImplementationContents = loaded.ImplementationContents
        };
    }

    // Add application ingress to both an invoking reaction and a producing reaction. Their command lives in an ungated sibling slice.
    static SemanticApplication WithInvocations(SemanticApplication application)
    {
        var module = application.Modules[0];
        var feature = module.Features[0];
        var command = feature.Slices.SelectMany(slice => slice.Commands).Single(command => command.Name == "RequestAlias");
        SemanticApplicationTrigger Trigger(string name) => new(SemanticId.Create(SemanticKind.Trigger, name), name, [.. command.Properties.Select(property => property with { Id = SemanticId.Create(SemanticKind.Property, name + "." + property.Name) })]);
        var invoking = Trigger("InvokeAlias");
        var producing = Trigger("InvokeProducingAlias");
        SemanticReactionTrigger Invoke(SemanticApplicationTrigger trigger) => new(SemanticReactionTriggerKind.ApplicationTrigger)
        {
            Source = trigger.Id,
            Invokes = [new(command.Id, [.. command.Properties.Select(property => new SemanticPropertyMapping(property.Id,
                new SemanticResolvedExpression(SemanticExpressionRootKind.Trigger, SemanticExpressionSourceKind.Property, trigger.Properties.Single(input => input.Name == property.Name).Id)))])]
        };
        SemanticSlice AddReactions(SemanticSlice slice) => slice.Name != "Assign" ? slice : slice with
        {
            Reactions =
            [
                slice.Reactions[0] with { Triggers = [.. slice.Reactions[0].Triggers, Invoke(producing)], RunsAs = new(SemanticReactionIdentityKind.System, []) },
                new(SemanticId.Create(SemanticKind.Reaction, "InvokeAlias"), "InvokeAlias", [Invoke(invoking)]) { RunsAs = new(SemanticReactionIdentityKind.System, []) }
            ]
        };
        return application with
        {
            Triggers = [invoking, producing],
            Modules = [module with { Features = [feature with { Slices = [.. feature.Slices.Select(AddReactions)] }] }]
        };
    }
}
