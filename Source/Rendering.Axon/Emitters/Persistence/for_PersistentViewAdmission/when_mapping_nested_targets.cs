// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence.for_PersistentViewAdmission;

public class when_mapping_nested_targets : given.a_flat_view
{
    AxonEmissionContext _context;
    ProjectionRecords _records;
    SemanticReadModel _board;
    SemanticId _details;
    SemanticId _note;
    SemanticId _cards;
    SemanticId _title;

    void Because()
    {
        var application = _request.Model.Application;
        _board = ScopeSelection.Select(_request).SelectMany(owner => owner.Slice.ReadModels).Single(model => model.Name == "Board");
        _details = _board.Properties.Single(property => property.Name == "details").Id;
        _cards = _board.Properties.Single(property => property.Name == "cards").Id;
        var details = application.Types.Single(type => type.Name == "BoardDetails");
        _note = details.Properties.Single(property => property.Name == "note").Id;
        _title = application.Types.Single(type => type.Name == "Card").Properties.Single(property => property.Name == "title").Id;
        _records = new(application, new(application, Java.JavaNames.PackageSegment(application.Name)));
        _context = Render(slice => slice.Name == "Boards" ? slice with
        {
            Specifications = [],
            Projections = [.. slice.Projections.Select(projection => projection with
            {
                Scope = projection.Scope! with
                {
                    Every = null, Children = [], Nested = [], Joins = [], Removals = [], JoinRemovals = [],
                    From = [projection.Scope.From[0] with { Mappings = [new([_details, _note], SemanticProjectionOperation.Set, new SemanticProjectionLiteral(new SemanticTextValue("created")))] }]
                }
            })]
        } : slice);
    }

    [Fact] void should_refuse_root_nested_targets() => PersistentViewAdmission.Supports(_context, ReadModelId("Board")).ShouldBeFalse();
    [Fact] void should_warn_without_crashing() => Code(_context, "BoardStoreConfiguration").ShouldContain("STAGE-AXON-0308");
    [Fact] void should_report_the_warning() => _context.Degradation.UnsupportedConstructs.Any(diagnostic => diagnostic.Artifact == ReadModelId("Board") && diagnostic.Code == "STAGE-AXON-0308").ShouldBeTrue();
    [Fact] void should_keep_the_memory_store() => Code(_context, "BoardStoreConfiguration").ShouldContain("InMemoryReadModelStore");
    [Fact] void should_not_emit_persistent_proofs() => _context.Artifacts.Exists(artifact => artifact.RelativePath.Contains("/BoardPersistent", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_resolve_the_leaf_property() => _records.Property(_board.Properties, [_details, _note])!.Id.ShouldEqual(_note);
    [Fact] void should_refuse_collection_segments() => (_records.Property(_board.Properties, [_cards, _title]) is null).ShouldBeTrue();
    [Fact] void should_refuse_unknown_segments() => (_records.Property(_board.Properties, [_details, _title]) is null).ShouldBeTrue();
    [Fact] void should_keep_the_collection_property_as_a_terminal_target() => _records.Property(_board.Properties, [_cards])!.Id.ShouldEqual(_cards);
    [Fact] void should_default_optional_composites() => Code(_context, "BoardProjectionProjector").ShouldContain("state.details() == null ? new BoardDetails(");
    [Fact] void should_keep_the_scoped_memory_handler() => Code(_context, "BoardProjectionProjector").ShouldContain("pending");
    [Fact] void should_keep_an_unadmitted_collection_scope_guarded() => Code(Render(slice => slice), "BoardStoreConfiguration").ShouldContain("STAGE-AXON-0308");
}
