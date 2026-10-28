// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonAutomationsEmitter;

public class when_a_trigger_member_name_is_a_java_keyword : given.a_reaction_model
{
    string code;

    void Because()
    {
        applicationTrigger = applicationTrigger with
        {
            Properties = [.. applicationTrigger.Properties.Select((property, index) => property with { Name = index == 0 ? "class" : "unchanged" + index })]
        };
        Render();
        code = Code("SubmitOrder");
    }

    [Fact] void should_escape_the_java_member() => code.ShouldContain(" class_");
    [Fact] void should_preserve_the_modelled_wire_name() => code.ShouldContain("@JsonProperty(\"class\") ");
    [Fact] void should_import_the_annotation() => code.ShouldContain("import com.fasterxml.jackson.annotation.JsonProperty;");
    [Fact] void should_not_annotate_unrenamed_members() => code.ShouldNotContain("@JsonProperty(\"unchanged");
}
