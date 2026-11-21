// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Screenplay.Semantics.Execution;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security.for_SecurityAnnotations;

public class when_a_producing_reaction_is_under_an_availability_gate : Specification
{
    const string Marker = "Event source does not match append boundary";

    static readonly ImmutableArray<SemanticAvailabilityRequirement> _gate = [new SemanticAudienceRequirement("Staff")];

    [Theory]
    [InlineData("module")]
    [InlineData("feature")]
    [InlineData("slice")]
    public async Task should_omit_the_synthesized_append_handler_through_the_production_planner(string owner)
    {
        var root = Path.Combine(for_AxonArtifactRenderPlanner.given.blueprint_models.Root(), "Source/Rendering.Axon.Verification/Corpus");
        var compilation = await SemanticModelLoader.LoadAsync(root, ["reaction-constraints.play"], null, "ReactionConstraints");
        var loaded = compilation.Loaded!;
        SemanticFeature Feature(SemanticFeature feature) => feature with
        {
            Available = owner == "feature" ? _gate : feature.Available,
            Slices = [.. feature.Slices.Select(slice => slice with { Available = owner == "slice" && slice.Name == "Assign" ? _gate : slice.Available })]
        };
        var application = loaded.Model.Application with
        {
            Audiences = [new("Staff")],
            Screens = [new(SemanticId.Create(SemanticKind.Screen, "Home"), "Home", "Home", [], [], [], new([], []))],
            Modules = [.. loaded.Model.Application.Modules.Select(module => module with
            {
                Available = owner == "module" ? _gate : module.Available,
                Features = [.. module.Features.Select(Feature)]
            })]
        };
        var model = ExecutableSemanticModel.Create(LanguageVersion.V56, SemanticVersion.V56, application);
        var request = new ArtifactRenderRequest(model, SemanticExecutionPlan.Compile(model).Plan!, AxonRendering.WithBearerTokens(AxonRendering.CreateProfile()), new(ArtifactRenderScopeKind.Application, application.Id))
        {
            ImplementationRequirements = loaded.ImplementationRequirements,
            ImplementationContents = loaded.ImplementationContents
        };

        var plan = new AxonArtifactRenderPlanner().Plan(request);

        plan.Refusals.ShouldBeEmpty();
        var handlers = plan.Artifacts.Select(artifact => Encoding.UTF8.GetString(artifact.Bytes.AsSpan())).Where(source => source.Contains(Marker, StringComparison.Ordinal)).ToArray();
        handlers.Length.ShouldEqual(owner == "slice" ? 1 : 0);
        handlers.Any(source => source.Contains("@Secured({})", StringComparison.Ordinal)).ShouldBeFalse();
        plan.Diagnostics.Any(diagnostic => diagnostic.Code == "STAGE-AXON-AUTHORIZATION" && diagnostic.Message.Contains("Availability gates", StringComparison.Ordinal)).ShouldBeTrue();
    }
}
