// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Queries;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence.for_AxonPersistenceEmitter;

public class when_a_model_claims_supplier : Projections.for_AxonProjectionsEmitter.given.a_view_request
{
    AxonEmissionContext _context;

    void Establish()
    {
        var application = _request.Model.Application;
        static SemanticFeature Rename(SemanticFeature feature) => feature with
        {
            Features = [.. feature.Features.Select(Rename)],
            Slices = [.. feature.Slices.Select(slice => slice with
            {
                ReadModels = [.. slice.ReadModels.Select(model => model.Name == "Total" ? model with { Name = "Supplier" } : model)]
            })]
        };
        _request = _request with
        {
            Model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application with
            {
                Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(Rename)] })]
            })
        };
    }

    void Because()
    {
        _context = new(_request, new(_request.Profile));
        new AxonProjectionsEmitter().Emit(_context);
        new AxonQueriesEmitter().Emit(_context);
        new AxonPersistenceEmitter().Emit(_context);
    }

    [Fact] void should_import_the_framework_supplier() => Support.ShouldContain("import java.util.function.Supplier;");
    [Fact] void should_use_the_framework_supplier_in_the_overload() => Support.ShouldContain("await(BooleanSupplier condition, Supplier<String> state)");
    [Fact] void should_qualify_the_modeled_supplier() => Support.ShouldContain("ReadModelStore<Object, axonviews.views.counters.totals.Supplier> store(");
    [Fact] void should_not_import_the_modeled_supplier() => Support.ShouldNotContain("import axonviews.views.counters.totals.Supplier;");
    [Fact] void should_not_use_the_modeled_supplier_by_short_name() => Support.ShouldNotContain("ReadModelStore<Object, Supplier>");

    string Support => Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/SupplierPersistentSupport.java", StringComparison.Ordinal)));
}
