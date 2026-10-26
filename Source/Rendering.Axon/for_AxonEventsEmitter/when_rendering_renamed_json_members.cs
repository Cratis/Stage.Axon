// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Events;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonEventsEmitter;

public class when_rendering_renamed_json_members : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string code;

    void Because()
    {
        var context = new AxonEmissionContext(request, new(request.Profile));
        var seed = ScopeSelection.Select(request).SelectMany(slice => slice.Slice.Events).First();
        var contract = seed with { Properties = [seed.Properties[0] with { Name = "class" }] };
        code = AxonEventSources.Record(context, "example.events", contract, "");
    }

    [Fact] void should_preserve_the_modeled_wire_name() => code.ShouldContain("@JsonProperty(\"class\")");
    [Fact] void should_import_the_wire_name_annotation() => code.ShouldContain("import com.fasterxml.jackson.annotation.JsonProperty;");
    [Fact] void should_use_a_legal_java_member_name() => code.ShouldContain(" class_");
}
