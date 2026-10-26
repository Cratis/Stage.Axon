// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views.for_NativeViewSpecifications;

public class when_a_reducer_model_claims_the_test_name : given.a_native_view_request
{
    void Because()
    {
        var model = _located.Slice.ReadModels.Single(model => model.Name == "Categories") with { Name = "Test" };
        ReducerViewSpecifications.Emit(_context, new ViewJavaTypes(_request.Model.Application), _located, model, [_spec]);
        _java = Text(_context.Artifacts.Single());
    }

    [Fact] void should_qualify_the_colliding_annotation() => _java.ShouldContain("@org.junit.jupiter.api.Test");
    [Fact] void should_not_import_the_colliding_annotation() => _java.ShouldNotContain("import org.junit.jupiter.api.Test;");
    [Fact] void should_shorten_the_other_annotation() => _java.ShouldContain("@Disabled(");
}
