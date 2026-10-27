// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Queries;

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence.for_PersistentViewAdmission.given;

public class a_flat_view : Projections.for_AxonProjectionsEmitter.given.a_view_request
{
    private protected AxonEmissionContext Render(Func<SemanticSlice, SemanticSlice> change)
    {
        SemanticFeature Feature(SemanticFeature feature) => feature with
        {
            Features = [.. feature.Features.Select(Feature)],
            Slices = [.. feature.Slices.Select(change)]
        };
        var application = _request.Model.Application;
        var model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application with { Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })] });
        var request = _request with { Model = model };
        var context = new AxonEmissionContext(request, new(request.Profile));
        new AxonProjectionsEmitter().Emit(context);
        new AxonQueriesEmitter().Emit(context);
        new AxonPersistenceEmitter().Emit(context);
        return context;
    }

    private protected static string Code(AxonEmissionContext context, string name) => Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith('/' + name + ".java", StringComparison.Ordinal)));
}
