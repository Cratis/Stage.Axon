// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_ReadModelRecords;

public class when_a_member_name_is_a_java_keyword : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string renamed;
    string regular;

    void Because()
    {
        var types = new ViewJavaTypes(request.Model.Application);
        var model = ScopeSelection.Select(request).SelectMany(slice => slice.Slice.ReadModels).First();
        var property = model.Properties[0];
        renamed = ReadModelRecords.Code(types, "example.views", model with { Name = "KeywordView", Properties = [property with { Name = "class" }] });
        regular = ReadModelRecords.Code(types, "example.views", model with { Name = "PlainView", Properties = [property with { Name = "orderId" }] });
    }

    [Fact] void should_preserve_the_modeled_wire_name() => renamed.ShouldContain("@JsonProperty(\"class\") ");
    [Fact] void should_import_the_wire_name_annotation() => renamed.ShouldContain("import com.fasterxml.jackson.annotation.JsonProperty;");
    [Fact] void should_escape_the_java_member() => renamed.ShouldContain(" class_");
    [Fact] void should_pass_the_escaped_member_to_the_copy_method() => renamed.ShouldContain("return new KeywordView(value);");
    [Fact] void should_not_annotate_an_unchanged_member() => regular.ShouldNotContain("JsonProperty");
}
