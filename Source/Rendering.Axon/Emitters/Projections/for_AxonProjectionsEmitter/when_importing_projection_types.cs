// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_importing_projection_types : given.a_view_request
{
    string _projector;
    string _configuration;
    string _flatProjector;
    string _collidingProjector;
    string _collidingModel;

    void Because()
    {
        var context = new AxonEmissionContext(_request, new(_request.Profile));
        new AxonProjectionsEmitter().Emit(context);
        _projector = Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/BoardProjectionProjector.java", StringComparison.Ordinal)));
        _configuration = Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/RebuiltTotalReducerConfiguration.java", StringComparison.Ordinal)));
        _flatProjector = Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/TotalProjectionProjector.java", StringComparison.Ordinal)));
        var application = _request.Model.Application;
        var model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application with
        {
            Types = [.. application.Types.Select(type => type.Name == "Card" ? type with { Name = "List" } : type)]
        });
        var collision = new AxonEmissionContext(_request with { Model = model }, new(_request.Profile));
        new AxonProjectionsEmitter().Emit(collision);
        _collidingProjector = Text(collision.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/BoardProjectionProjector.java", StringComparison.Ordinal)));
        _collidingModel = Text(collision.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/Board.java", StringComparison.Ordinal)));
    }

    [Fact] void should_shorten_persistence_and_query_update_parameters() => _flatProjector.ShouldContain("public void on(Increased event, QueryUpdateEmitter updates)");
    [Fact] void should_import_the_update_emitter_for_the_binding() => _flatProjector.ShouldContain("import org.axonframework.messaging.queryhandling.QueryUpdateEmitter;");
    [Fact] void should_import_nested_records() => _projector.ShouldContain("import axonviews.types.BoardDetails;");
    [Fact] void should_import_child_records() => _projector.ShouldContain("import axonviews.types.Card;");
    [Fact] void should_import_concepts_for_keys() => _projector.ShouldContain("import axonviews.concepts.BoardId;");
    [Fact] void should_preserve_unwrapped_event_keys() => _projector.ShouldContain("String.valueOf(event.boardId().value())");
    [Fact] void should_construct_short_nested_records() => _projector.ShouldContain("new BoardDetails(");
    [Fact] void should_type_child_methods_with_short_names() => _projector.ShouldContain("private ScopeChange<Card> applyRootChild0");
    [Fact] void should_type_join_caches_with_short_names() => _projector.ShouldContain("Map<String, UnaryOperator<Card>>");
    [Fact] void should_type_child_collections_with_short_names() => _projector.ShouldContain("new ArrayList<Card>()");
    [Fact] void should_not_construct_qualified_application_types() => _projector.ShouldNotContain("new axonviews.");
    [Fact] void should_import_reducer_bean_annotations() => _configuration.ShouldContain("import org.springframework.context.annotation.Bean;");
    [Fact] void should_use_short_reducer_bean_annotations() => _configuration.ShouldContain("@Bean(\"axonviews.reduction.counters.rebuilt.RebuiltTotalReducer\")");
    [Fact] void should_import_a_colliding_application_record() => _collidingProjector.ShouldContain("import axonviews.types.List;");
    [Fact] void should_not_import_the_colliding_framework_type() => _collidingProjector.ShouldNotContain("import java.util.List;");
    [Fact] void should_keep_framework_collections_qualified() => _collidingProjector.ShouldContain("private final java.util.List<Predicate<ReadModelStore<Object, Board>>> pending");
    [Fact] void should_keep_application_children_short() => _collidingProjector.ShouldContain("new ArrayList<List>()");
    [Fact] void should_keep_nested_collection_types_unambiguous() => _collidingModel.ShouldContain("java.util.List<List> cards");
}
