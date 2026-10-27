// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter;

public class when_a_command_response_field_is_a_java_keyword : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string code;

    void Because()
    {
        var seed = ScopeSelection.Select(request).First(slice => !slice.Slice.Commands.IsEmpty).Slice.Commands[0];
        var property = seed.Properties[0];
        var context = new AxonEmissionContext(request, new(request.Profile));
        var responses = new CommandResponses(seed with { Response = new SemanticRecordCommandResponse([new(Name: "class", Type: property.Type, Source: property.Id)]) }, new CommandJavaTypes(request.Model.Application));
        responses.Emit(context, "example.orders");
        code = Encoding.UTF8.GetString(context.Artifacts.Single().Bytes.AsSpan());
    }

    [Fact] void should_preserve_the_modeled_wire_name() => code.ShouldContain("@JsonProperty(\"class\") ");
    [Fact] void should_import_the_wire_name_annotation() => code.ShouldContain("import com.fasterxml.jackson.annotation.JsonProperty;");
    [Fact] void should_escape_the_java_member() => code.ShouldContain(" class_");
}
