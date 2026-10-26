// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views.for_NativeViewSpecifications;

public class when_a_populated_given_concept_is_named_read_model_store : for_AxonViewSpecsEmitter.given.a_populated_concept_collision
{
    void Because()
    {
        Prepare(true);
        new AxonViewSpecsEmitter().Emit(_context);
        _java = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/CategoriesViewSpecificationsTest.java", StringComparison.Ordinal)));
    }

    [Fact] void should_import_the_application_concept() => _java.ShouldContain("import nativeviews.concepts.ReadModelStore;");
    [Fact] void should_construct_the_populated_concept() => _java.ShouldContain("new ReadModelStore(\"visible\")");
    [Fact] void should_qualify_the_runtime_store() => _java.ShouldContain("nativeviews.runtime.ReadModelStore<Object, Categories> store");
    [Fact] void should_not_import_the_runtime_store() => _java.ShouldNotContain("import nativeviews.runtime.ReadModelStore;");
    [Fact] void should_keep_the_native_authentication_fixture() => _java.ShouldContain("SecurityContextHolder.clearContext();");
}
