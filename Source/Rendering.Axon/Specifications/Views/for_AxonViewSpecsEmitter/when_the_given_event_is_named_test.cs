// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views.for_AxonViewSpecsEmitter;

public class when_the_given_event_is_named_test : given.a_populated_concept_collision
{
    void Because()
    {
        Prepare(false, eventName: "Test");
        new AxonViewSpecsEmitter().Emit(_context);
        _java = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/CategoriesViewSpecificationsTest.java", StringComparison.Ordinal)));
    }

    [Fact] void should_import_the_application_event() => _java.ShouldContain("import nativeviews.catalog.events.Test;");
    [Fact] void should_construct_the_short_application_event() => _java.ShouldContain("projector.on(new Test(");
    [Fact] void should_qualify_the_framework_annotation() => _java.ShouldContain("@org.junit.jupiter.api.Test");
    [Fact] void should_not_import_the_framework_annotation() => _java.ShouldNotContain("import org.junit.jupiter.api.Test;");
}
