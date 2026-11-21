// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security.for_SecurityAnnotations;

public class when_operations_are_under_an_inherited_availability_gate : Specification
{
    static readonly ImmutableArray<SemanticAvailabilityRequirement> _gate = [new SemanticAudienceRequirement("Staff")];

    static readonly string[] _operations = ["RegisterAccount", "PerformedAccounts", "LivePerformedAccounts"];

    [Theory]
    [InlineData("module")]
    [InlineData("feature")]
    [InlineData("slice")]
    [InlineData("module-feature-slice")]
    public async Task should_deny_every_command_and_snapshot_and_live_performed_query_and_warn(string owner)
    {
        var root = Path.Combine(for_AxonArtifactRenderPlanner.given.blueprint_models.Root(), "Source/Rendering.Axon.Verification/Corpus");
        var compilation = await SemanticModelLoader.LoadAsync(root, ["inline-java-policy-performers.play"], null, "InlinePolicies");
        var loaded = compilation.Loaded!;
        bool Gate(string level) => owner == level || owner == "module-feature-slice";
        SemanticFeature Feature(SemanticFeature feature) => feature with
        {
            Available = Gate("feature") ? _gate : feature.Available,
            Features = [.. feature.Features.Select(Feature)],
            Slices = [.. feature.Slices.Select(slice => slice with { Available = Gate("slice") ? _gate : slice.Available })]
        };
        var application = loaded.Model.Application with
        {
            Audiences = [new("Staff")],
            Modules = [.. loaded.Model.Application.Modules.Select(module => module with
            {
                Available = Gate("module") ? _gate : module.Available,
                Features = [.. module.Features.Select(Feature)]
            })]
        };
        var model = ExecutableSemanticModel.Create(LanguageVersion.V53, SemanticVersion.V53, application);
        var request = new ArtifactRenderRequest(model, loaded.Plan, AxonRendering.WithBearerTokens(AxonRendering.CreateProfile()), new(ArtifactRenderScopeKind.Application, application.Id))
        {
            ImplementationRequirements = loaded.ImplementationRequirements,
            ImplementationContents = loaded.ImplementationContents
        };

        var context = new AxonEmissionContext(request, new(request.Profile));
        var decisions = AxonUnsupportedSemantics.Decisions(request, "example").Where(decision => decision.Code == "STAGE-AXON-AUTHORIZATION").ToArray();
        AxonBehaviorAdmission.Apply(context, decisions);
        foreach (var emitter in AxonEmitters.Ordered.Where(candidate => candidate.IsImplemented)) emitter.Emit(context);
        string Source(string name) => Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)).Bytes.AsSpan());
        foreach (var operation in _operations)
        {
            Source(operation + "Endpoint").ShouldContain("@PreAuthorize(\"denyAll()\")");
            Source(operation + "Handler").ShouldContain("@Secured({})");
        }

        var gated = ScopeSelection.Select(request).SelectMany(located => AvailabilityGates.Gated(request, located)).ToArray();
        gated.Length.ShouldEqual(_operations.Length);
        decisions.Select(decision => decision.Owner).ShouldContainOnly(gated);
        var reported = context.Degradation.UnsupportedConstructs.Concat(context.Degradation.Warnings).Where(diagnostic => diagnostic.Code == "STAGE-AXON-AUTHORIZATION").Select(diagnostic => diagnostic.Artifact.ToString()).ToArray();
        foreach (var id in gated) reported.ShouldContain(id.ToString());
    }
}
