// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views.for_AxonViewHttpSpecs;

public class when_fragment_type_names_collide : given.a_modeled_http_query
{
    [Theory]
    [InlineData("List", "java.util.List")]
    [InlineData("Assertions", "org.junit.jupiter.api.Assertions")]
    [InlineData("JsonMapper", "tools.jackson.databind.json.JsonMapper")]
    [InlineData("TypeReference", "tools.jackson.core.type.TypeReference")]
    [InlineData("ReadModelStore", "axonviews.runtime.ReadModelStore")]
    public void should_give_the_modeled_type_the_short_name(string name, string framework)
    {
        var code = Render(slice => slice with { ReadModels = [.. slice.ReadModels.Select(model => model.Name == "Total" ? model with { Name = name } : model)] });
        code.ShouldContain("import axonviews.views.counters.totals." + name + ";");
        code.ShouldNotContain("import " + framework + ";");
        code.ShouldContain("<" + name + ">>() {}");
        var suffix = name switch { "List" or "TypeReference" => "<", "Assertions" => ".assertTrue(", _ => ".class" };
        code.ShouldContain(framework + suffix);
    }

    [Fact]
    void should_preserve_the_model_name_when_an_event_has_the_same_simple_name()
    {
        var code = Render(slice => slice with { ReadModels = [.. slice.ReadModels.Select(model => model.Name == "Total" ? model with { Name = "Increased" } : model)] });
        code.ShouldContain("import axonviews.views.counters.totals.Increased;");
        code.ShouldContain("new TypeReference<List<Increased>>() {}");
        code.ShouldContain("projector.on(new axonviews.views.events.Increased(");
        code.ShouldNotContain("import axonviews.views.events.Increased;");
    }

    [Fact]
    void should_preserve_the_projector_name_when_an_event_has_the_same_simple_name()
    {
        var code = Render(slice => slice with { Events = [.. slice.Events.Select(contract => contract.Name == "Increased" ? contract with { Name = "TotalProjectionProjector" } : contract)] });
        code.ShouldContain("import axonviews.views.counters.totals.TotalProjectionProjector;");
        code.ShouldContain("application.getBean(TotalProjectionProjector.class)");
        code.ShouldContain("projector.on(new axonviews.views.events.TotalProjectionProjector(");
        code.ShouldNotContain("import axonviews.views.events.TotalProjectionProjector;");
    }

    [Fact]
    void should_give_an_event_priority_over_a_framework_annotation()
    {
        var code = Render(slice => slice with { Events = [.. slice.Events.Select(contract => contract.Name == "Increased" ? contract with { Name = "Test" } : contract)] });
        code.ShouldContain("import axonviews.views.events.Test;");
        code.ShouldContain("projector.on(new Test(");
        code.ShouldContain("@org.junit.jupiter.api.Test");
        code.ShouldNotContain("import org.junit.jupiter.api.Test;");
    }
}
