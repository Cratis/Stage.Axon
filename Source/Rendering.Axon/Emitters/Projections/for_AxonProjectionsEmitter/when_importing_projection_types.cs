// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_importing_projection_types : given.a_view_request
{
    string _projector;
    string _reducer;
    string _flatProjector;
    string _collidingProjector;
    string _collidingModel;

    void Because()
    {
        var context = new AxonEmissionContext(_request, new(_request.Profile));
        new AxonProjectionsEmitter().Emit(context);
        _projector = Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/BoardProjectionProjector.java", StringComparison.Ordinal)));
        _reducer = Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/RebuiltTotalReducer.java", StringComparison.Ordinal)));
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

    [Fact] void should_shorten_persistence_and_query_update_parameters() => _flatProjector.ShouldContain("public synchronized void on(Increased event, QueryUpdateEmitter updates)");
    [Fact] void should_import_the_update_emitter_for_the_binding() => _flatProjector.ShouldContain("import org.axonframework.messaging.queryhandling.QueryUpdateEmitter;");
    [Fact] void should_degrade_the_scoped_projection_to_an_empty_projector() => _projector.ShouldContain("STAGE-AXON-0310");
    [Fact] void should_not_construct_qualified_application_types() => _projector.ShouldNotContain("new axonviews.");
    [Fact] void should_import_reducer_component_annotations() => _reducer.ShouldContain("import org.springframework.stereotype.Component;");
    [Fact] void should_use_short_reducer_component_annotations() => _reducer.ShouldContain("@Component(\"axonviews.reduction.counters.rebuilt.RebuiltTotalReducer\")");
    [Fact] void should_keep_nested_collection_types_unambiguous() => _collidingModel.ShouldContain("java.util.List<List> cards");
}
