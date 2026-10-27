// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Queries;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence.for_AxonPersistenceEmitter;

public class when_proofs_share_guarded_processors : Projections.for_AxonProjectionsEmitter.given.a_view_request
{
    AxonEmissionContext _context;

    void Because()
    {
        _context = new(_request, new(_request.Profile));
        new AxonProjectionsEmitter().Emit(_context);
        new AxonQueriesEmitter().Emit(_context);
        new AxonPersistenceEmitter().Emit(_context);
    }

    [Fact]
    void should_import_every_guarded_projector_in_each_persistent_fixture()
    {
        var models = ScopeSelection.Select(_request).SelectMany(owner => owner.Slice.ReadModels
            .Where(model => PersistentViewAdmission.Supports(_context, model.Id))
            .Select(model => (Owner: owner, Model: model))).ToArray();
        models.Length.ShouldBeGreaterThan(0);
        var types = new ViewJavaTypes(_request.Model.Application);
        var supports = _context.Artifacts.Where(artifact => artifact.RelativePath.EndsWith("PersistentSupport.java", StringComparison.Ordinal)).ToArray();
        supports.Length.ShouldBeGreaterThan(0);
        foreach (var support in supports)
        {
            var fixture = Text(support).Split("@Import({", StringSplitOptions.None)[1];
            foreach (var (owner, model) in models)
            {
                var projection = owner.Slice.Projections.FirstOrDefault(candidate => candidate.ReadModel == model.Id);
                var projector = projection is null ? model.Name + "ReducerProjector" : projection.Name + "Projector";
                fixture.ShouldContain(projector + ".class");
                fixture.ShouldContain(model.Name + "StoreConfiguration.class");
                if (projection is null) fixture.ShouldContain(model.Name + "ReducerConfiguration.class");
                Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/PersistentViewConfiguration.java", StringComparison.Ordinal)))
                    .ShouldContain("pooledStreamingMatching(\"" + types.Package(owner) + "\")");
            }
        }
    }
}
