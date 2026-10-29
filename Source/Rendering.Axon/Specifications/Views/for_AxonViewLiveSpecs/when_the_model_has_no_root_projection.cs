// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views.for_AxonViewLiveSpecs;

public class when_the_model_has_no_root_projection : for_NativeViewSpecifications.given.a_native_view_request
{
    async Task Establish()
    {
        _request = await Load("axon-samples/views");
        _context = new(_request, new(_request.Profile));
        _located = ScopeSelection.Select(_request).Single(slice => slice.Slice.Queries.Any(query => query.Name == "LiveTotals"));
    }

    void Because()
    {
        var query = _located.Slice.Queries.Single(query => query.Name == "LiveTotals");
        var model = _located.Slice.ReadModels.Single(model => model.Id == query.ReadModel);
        var owner = _located with { Slice = _located.Slice with { Projections = [] } };
        AxonViewLiveSpecs.Emit(_context, new ViewJavaTypes(_request.Model.Application, Java.JavaNames.PackageSegment(_request.Model.Application.Name)), _located, query, owner, model);
    }

    [Fact] void should_skip_the_root_specific_probe_without_throwing() => _context.Artifacts.ShouldBeEmpty();
}
