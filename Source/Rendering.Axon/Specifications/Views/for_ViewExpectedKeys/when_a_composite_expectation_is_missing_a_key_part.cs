// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Java;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views.for_ViewExpectedKeys;

public class when_a_composite_expectation_is_missing_a_key_part : for_NativeViewSpecifications.given.a_native_view_request
{
    string _expression;
    bool _supported;

    void Because()
    {
        var original = _located.Slice.ReadModels.Single();
        var model = original with { Properties = [.. original.Properties.Select(property => property with { IsIdentifier = true })] };
        var identifier = original.Properties.Single(property => property.IsIdentifier);
        var key = new SemanticCompositeValue([new(identifier.Id, new SemanticTextValue("6f1f4a3e-0c0d-4c6a-9a41-3b6a1a0e0021"))]);
        var types = new ViewJavaTypes(_request.Model.Application, JavaNames.PackageSegment(_request.Model.Application.Name));
        _expression = ViewExpectedKeys.Expression(key, model, types);
        _supported = ViewSpecificationSupport.Legacy(_spec with { ThenReadModels = [], ThenAbsentReadModels = [new(model.Id, key)] }, model, types);
    }

    [Fact] void should_return_the_same_missing_key_sentinel_as_a_single_key() => _expression.ShouldBeNull();
    [Fact] void should_not_admit_an_incomplete_composite_absence_expectation() => _supported.ShouldBeFalse();
}
