// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Verification.for_ConstraintProofArtifacts;

public class when_planning_the_constraint_applications : Specification
{
    readonly Dictionary<string, ArtifactRenderPlan> plans = [];

    async Task Establish()
    {
        var repository = for_CorpusApplications.given.the_repository.Root();
        foreach (var application in new[] { ConstraintProofArtifacts.Application, ConstraintProofArtifacts.PersistentApplication })
        {
            var name = "Corpus" + application.Replace("-", string.Empty, StringComparison.Ordinal);
            var compilation = await SemanticModelLoader.LoadAsync(Path.Combine(repository, "Source/Rendering.Axon.Verification/Corpus"), ["constrained-java-bodies.play"], null, name);
            compilation.Success.ShouldBeTrue();
            var loaded = compilation.Loaded;
            plans[application] = application == ConstraintProofArtifacts.PersistentApplication
                ? ConstraintProofArtifacts.PersistentPlan(loaded, CorpusRenderer.Profile)
                : AxonRendering.PlanFrom(loaded, new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id), profile: CorpusRenderer.Profile);
        }
    }

    [Theory]
    [InlineData(ConstraintProofArtifacts.Application)]
    [InlineData(ConstraintProofArtifacts.PersistentApplication)]
    public void should_plan_without_refusals(string application) => (plans[application].Success && plans[application].Refusals.IsEmpty).ShouldBeTrue();

    [Theory]
    [InlineData(ConstraintProofArtifacts.Application)]
    [InlineData(ConstraintProofArtifacts.PersistentApplication)]
    public void should_enforce_occurrences_for_both_handler_paths(string application)
    {
        Source(application, "ModeledConstraints").ShouldContain("store.occurOnce(context,");
        Source(application, "OpenAccountHandler").ShouldContain("ModeledConstraints.enforcing(uniqueValues, unconstrainedAppender, context)");
        Source(application, "OpenAccountModeledHandler").ShouldContain("ModeledConstraints.apply(uniqueValues, event, context)");
    }

    [Theory]
    [InlineData(ConstraintProofArtifacts.Application)]
    [InlineData(ConstraintProofArtifacts.PersistentApplication)]
    public void should_preserve_native_bodies_without_degradation(string application)
    {
        Source(application, "ClaimAliasHandler").ShouldContain("appender.append(new AliasClaimed(command.id(), command.id(), command.alias()));");
        plans[application].Diagnostics.Any(diagnostic => new[] { "STAGE-AXON-0801", "STAGE-AXON-0401", "STAGE-AXON-IMPL", "STAGE-AXON-SPEC" }.Contains(diagnostic.Code)).ShouldBeFalse();
        plans[application].Artifacts.Any(artifact => artifact.RelativePath.Contains("/src/test/", StringComparison.Ordinal) && Text(artifact).Contains("@Disabled", StringComparison.Ordinal)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(ConstraintProofArtifacts.Application)]
    [InlineData(ConstraintProofArtifacts.PersistentApplication)]
    public void should_resolve_proof_imports_to_generated_types(string application)
    {
        var package = "corpus" + application.Replace("-", string.Empty, StringComparison.Ordinal);
        foreach (var source in ConstraintProofArtifacts.JavaSources(application).Values)
        {
            foreach (var line in source.Split('\n').Where(line => line.StartsWith("import " + package + ".", StringComparison.Ordinal)))
            {
                var path = "/" + line[7..^1].Replace('.', '/') + ".java";
                Assert.True(plans[application].Artifacts.Any(artifact => artifact.RelativePath.EndsWith(path, StringComparison.Ordinal)), line);
            }
        }
    }

    string Source(string application, string name) => Text(plans[application].Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)));

    static string Text(PlannedArtifact artifact) => Encoding.UTF8.GetString(artifact.Bytes.AsSpan());
}
