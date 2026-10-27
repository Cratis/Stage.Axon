// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter.given;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter.given;

public class a_query_collision : a_view_request
{
    protected SemanticKeyedQuery _query;
    protected SemanticReadModel _model;
    protected SemanticReadModel[] _additionalModels = [];
    private protected AxonEmissionContext _context;
    protected string _package;

    void Establish()
    {
        var located = ScopeSelection.Select(_request).Single(slice => slice.Slice.Queries.Any(query => query.Name == "FindTotal"));
        _query = located.Slice.Queries.Single(query => query.Name == "FindTotal");
        _model = located.Slice.ReadModels.Single(model => model.Id == _query.ReadModel);
        _package = new ViewJavaTypes(_request.Model.Application, Java.JavaNames.PackageSegment(_request.Model.Application.Name)).Package(located);
    }

    protected void Render(SemanticConcept? concept = null)
    {
        var application = _request.Model.Application;
        application = application with
        {
            Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(Update)] })],
            Concepts = concept is null ? application.Concepts : application.Concepts.Add(concept)
        };
        _request = _request with { Model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application) };
        _context = new(_request, new(_request.Profile));
        new AxonQueriesEmitter().Emit(_context);
    }

    protected string Code(string name) => Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith('/' + name + ".java", StringComparison.Ordinal)));

    SemanticFeature Update(SemanticFeature feature) => feature with
    {
        Features = [.. feature.Features.Select(Update)],
        Slices = [.. feature.Slices.Select(slice => slice with
        {
            Queries = [.. slice.Queries.Select(query => query.Id == _query.Id ? _query : query)],
            ReadModels = [.. slice.ReadModels.Select(model => model.Id == _model.Id ? _model : model), .. slice.ReadModels.Any(model => model.Id == _model.Id) ? _additionalModels : []]
        })]
    };
}
