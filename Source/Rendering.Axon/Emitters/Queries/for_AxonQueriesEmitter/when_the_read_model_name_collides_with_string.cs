// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter.given;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter;

public class when_the_read_model_name_collides_with_string : a_view_request
{
    string _endpoint;

    void Because()
    {
        var located = ScopeSelection.Select(_request).Single(slice => slice.Slice.Queries.Any(query => query.Name == "FindTotal"));
        var query = located.Slice.Queries.Single(query => query.Name == "FindTotal");
        var model = located.Slice.ReadModels.Single(model => model.Id == query.ReadModel);
        var types = new ViewJavaTypes(_request.Model.Application);
        var context = new AxonEmissionContext(_request, new(_request.Profile));
        new QueryEndpoint(context, types, located, query, "axonviews.types.String", new(query, model, types)).Emit();
        _endpoint = Text(context.Artifacts.Single());
    }

    [Fact] void should_import_the_application_read_model() => _endpoint.ShouldContain("import axonviews.types.String;");
    [Fact] void should_keep_the_read_model_reference_short() => _endpoint.ShouldContain("CompletableFuture<ResponseEntity<String>>");
    [Fact] void should_qualify_the_primitive_request_parameter() => _endpoint.ShouldContain("required = true) java.lang.String name");
    [Fact] void should_not_import_java_lang_string() => _endpoint.ShouldNotContain("import java.lang.String;");
}
