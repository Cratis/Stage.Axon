// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Queries;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security.for_SecurityAnnotations;

public class when_a_performed_query_is_reached_from_two_slices : Specification
{
    AxonEmissionContext _context;
    SemanticImplementationRequirement[] _queries;

    async Task Establish()
    {
        var root = Path.Combine(for_AxonArtifactRenderPlanner.given.blueprint_models.Root(), "Source/Rendering.Axon.Verification/Corpus");
        var loaded = await SemanticModelLoader.LoadFromPathAsync(Path.Combine(root, "inline-java-policy-performers.play"), null, "InlinePolicies");
        var application = loaded.Model.Application;
        var module = application.Modules.Single();
        var feature = module.Features.Single();
        var active = feature.Slices.Single(slice => slice.ReadModels.Any());
        var otherModel = active.ReadModels.Single() with
        {
            Id = SemanticId.Create(SemanticKind.ReadModel, "Accounts.Registry.OtherActive.OtherAccounts"),
            Name = "OtherAccounts",
            Properties = [.. active.ReadModels.Single().Properties.Select(property => property with { Id = SemanticId.Create(SemanticKind.Property, "OtherAccounts." + property.Name) })]
        };
        var other = active with
        {
            Id = SemanticId.Create(SemanticKind.Slice, "Accounts.Registry.OtherActive"),
            Name = "OtherActive",
            ReadModels = [otherModel],
            Projections = []
        };
        application = application with { Modules = [module with { Features = [feature with { Slices = [.. feature.Slices, other] }] }] };
        var model = ExecutableSemanticModel.Create(loaded.Model.LanguageVersion, loaded.Model.SemanticVersion, application);
        _queries = [.. loaded.ImplementationRequirements.Where(requirement => requirement.Role == SemanticImplementationRole.QueryPerformer)];
        var request = new ArtifactRenderRequest(model, loaded.Plan, AxonRendering.CreateProfile(), new(ArtifactRenderScopeKind.Application, application.Id))
        {
            ImplementationRequirements = [.. loaded.ImplementationRequirements, .. _queries.Select(query => query with { QueryShape = query.QueryShape! with { ReadModel = otherModel.Id } })],
            ImplementationContents = loaded.ImplementationContents
        };
        _context = new(request, new(request.Profile));
        _queries.Length.ShouldEqual(2);
        foreach (var query in _queries)
        {
            ScopeSelection.Select(request).Count(slice => QueryPerformer.Queries(_context, slice).Any(candidate => candidate.Id == query.Source.SemanticId)).ShouldEqual(2);
        }
    }

    void Because()
    {
        new AxonSecurityEmitter().Emit(_context);
        new AxonQueriesEmitter().Emit(_context);
    }

    [Fact]
    void should_declare_each_authorization_method_exactly_once()
    {
        var policies = Encoding.UTF8.GetString(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/Policies.java", StringComparison.Ordinal)).Bytes.AsSpan());
        foreach (var query in _queries)
        {
            var declaration = "public boolean " + PolicyPredicates.SiteMember(query.Source.SemanticId) + "(Authentication authentication)";
            (policies.Split(declaration, StringSplitOptions.None).Length - 1).ShouldEqual(1);
        }
    }

    [Fact]
    void should_emit_each_query_artifact_exactly_once()
    {
        foreach (var query in _queries)
        {
            foreach (var suffix in new[] { "", "Handler", "Endpoint", "Performer", "PerformerConfiguration" })
            {
                _context.Artifacts.Count(artifact => artifact.RelativePath.EndsWith("/" + query.Owner.Name + suffix + ".java", StringComparison.Ordinal)).ShouldEqual(1);
            }
        }
    }
}
