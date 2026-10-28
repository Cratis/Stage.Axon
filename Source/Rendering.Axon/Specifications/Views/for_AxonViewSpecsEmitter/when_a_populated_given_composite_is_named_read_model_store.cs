// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views.for_AxonViewSpecsEmitter;

public class when_a_populated_given_composite_is_named_read_model_store : given.a_populated_composite_collision
{
    void Because()
    {
        PrepareComposite();
        new AxonViewSpecsEmitter().Emit(_context);
        _java = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/CategoriesViewSpecificationsTest.java", StringComparison.Ordinal)));
    }

    [Fact] void should_import_the_application_composite() => _java.ShouldContain("import nativeviews.types.ReadModelStore;");
    [Fact] void should_import_the_nested_concept() => _java.ShouldContain("import nativeviews.concepts.StatusValue;");
    [Fact] void should_construct_the_populated_composite() => _java.ShouldContain("new ReadModelStore(new StatusValue(\"visible\"))");
    [Fact] void should_type_the_repository_with_the_model() => _java.ShouldContain("CategoriesRepository repository");
    [Fact] void should_not_import_the_removed_runtime_store() => _java.ShouldNotContain("import io.cratis.stage.axon.runtime.ReadModelStore;");
}
