// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_emitting_scoped_views : given.a_view_request
{
    string _projector;
    string _entity;
    string _joinEntity;
    string _joinRepository;

    void Because()
    {
        var context = new AxonEmissionContext(_request, new(_request.Profile));
        new AxonProjectionsEmitter().Emit(context);
        string Artifact(string name) => Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)));
        _projector = Artifact("BoardProjectionProjector");
        _entity = Artifact("BoardEntity");
        _joinEntity = Artifact("BoardProjectionJoinEntity");
        _joinRepository = Artifact("BoardProjectionJoinRepository");
    }

    [Fact] void should_not_degrade_to_the_scoped_stub() => _projector.ShouldNotContain("STAGE-AXON-0310");
    [Fact] void should_render_a_single_projector_component() => _projector.ShouldContain("@Component(\"axonviews.scopes.boards.boards.BoardProjectionProjector\")");
    [Fact] void should_handle_the_child_event_through_an_event_handler() => _projector.ShouldContain("public void handle(CardAdded event, ProcessingContext context)");
    [Fact] void should_not_keep_scope_state() => _projector.ShouldNotContain("pending");

    [Fact] void should_store_children_as_a_json_column() => _entity.ShouldContain("private List<Card> cards;");
    [Fact] void should_map_the_children_column() => _entity.ShouldContain("@Column(name = \"p_cards\")");
    [Fact] void should_upsert_the_parent_row_when_a_child_arrives_first() => _projector.ShouldContain("if (current == null && !(false || Objects.equals(key, String.valueOf(event.boardId().value())))) return");
    [Fact] void should_read_the_parent_row_by_key() => _projector.ShouldContain("keys.forEach(candidate -> states.putIfAbsent(candidate, repository.findById(candidate).map(BoardEntity::toModel).orElse(null)));");
    [Fact] void should_save_the_touched_row() => _projector.ShouldContain("else repository.save(new BoardEntity(key, change.value()));");

    [Fact] void should_remember_the_join_event_in_the_lookup_table() => _projector.ShouldContain("joins.save(new BoardProjectionJoinEntity(\"join0|\" + String.valueOf(String.valueOf(event.cardId().value()))).join0(event));");
    [Fact] void should_hydrate_children_from_the_lookup_table() => _projector.ShouldContain("var joined = joins.findById(\"join0|\" + String.valueOf(");
    [Fact] void should_keep_the_join_event_in_a_json_column() => _joinEntity.ShouldContain("private CardRenamed join0;");
    [Fact] void should_declare_a_string_keyed_join_repository() => _joinRepository.ShouldContain("extends JpaRepository<BoardProjectionJoinEntity, String>");
    [Fact] void should_scan_rows_for_join_events() => _projector.ShouldContain("for (var row : repository.findAll()) states.put(row.rowKey(), row.toModel());");

    [Fact] void should_remove_rows_with_a_batch_delete_by_key() => _projector.ShouldContain("if (change.value() == null) repository.deleteAllByIdInBatch(List.of(key));");
    [Fact] void should_not_remove_rows_by_finding_them_first() => _projector.ShouldNotContain("repository.deleteById(");
}
