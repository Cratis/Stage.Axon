// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Automations;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.for_AxonAutomationsEmitter.given;

public class a_reaction_model : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    private protected AxonEmissionContext context;
    private protected SemanticReaction reaction;
    private protected SemanticCommand command;
    private protected SemanticApplicationTrigger applicationTrigger;

    void Establish()
    {
        command = ScopeSelection.Select(request).First(slice => !slice.Slice.Commands.IsEmpty).Slice.Commands[0] with { Authorization = null };
        applicationTrigger = new(SemanticId.Create(SemanticKind.Trigger, "SubmitOrder"), "SubmitOrder", [.. command.Properties.Select(property => property with { Id = SemanticId.Create(SemanticKind.Property, "SubmitOrder." + property.Name) })]);
        var invocation = new SemanticInvocation(command.Id, [.. command.Properties.Select(property => new SemanticPropertyMapping(property.Id, new SemanticResolvedExpression(SemanticExpressionRootKind.Trigger, SemanticExpressionSourceKind.Property, applicationTrigger.Properties.Single(input => input.Name == property.Name).Id)))]);
        reaction = new(SemanticId.Create(SemanticKind.Reaction, "SubmitOrder"), "SubmitOrder", [new(SemanticReactionTriggerKind.ApplicationTrigger) { Source = applicationTrigger.Id, Invokes = [invocation] }]);
    }

    private protected void Render()
    {
        var application = request.Model.Application;
        var modules = application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] }).ToImmutableArray();
        request = request with { Model = ExecutableSemanticModel.Create(reaction.RunsAs is null ? LanguageVersion.V6 : LanguageVersion.V10, reaction.RunsAs is null ? SemanticVersion.V6 : SemanticVersion.V10, application with { Modules = modules, Triggers = [applicationTrigger] }) };
        context = new(request, new(request.Profile));
        new AxonAutomationsEmitter().Emit(context);
    }

    private protected string Code(string name) => Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith('/' + name + ".java", StringComparison.Ordinal)).Bytes.AsSpan());

    SemanticFeature Feature(SemanticFeature feature) => feature with
    {
        Features = [.. feature.Features.Select(Feature)],
        Slices = [.. feature.Slices.SelectMany(slice => slice.Commands.Any(candidate => candidate.Id == command.Id)
            ? new[] { slice with { Commands = [command] }, slice with
            {
                Id = SemanticId.Create(SemanticKind.Slice, "SubmitOrder"), Name = "SubmitOrder", Kind = SemanticSliceKind.Automation,
                Commands = [], Events = [], Constraints = [], ReadModels = [], Projections = [], Queries = [], Specifications = [], Reducers = [], Captures = [], Reactions = [reaction]
            } } : [slice])]
    };
}
