// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter;

public class when_an_allocating_command_is_named_event_source_id : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string _code;

    void Because()
    {
        var located = ScopeSelection.Select(request).First(slice => !slice.Slice.Commands.IsEmpty);
        var command = located.Slice.Commands[0] with { Name = "EventSourceId", Properties = [] };
        var context = new AxonEmissionContext(request, new(request.Profile));
        var source = new CommandSource
        {
            Primary = ("eventSourceId", context.BasePackage + ".runtime.EventSourceId", "eventSourceId"),
            Type = SemanticTypeReference.ForPrimitive(SemanticPrimitiveType.Text),
            TagReference = "EventTags.EVENT_SOURCE_ID"
        };
        var package = CommandContractsEmitter.Package(new(request.Model.Application), located);
        _code = CommandContractsEmitter.Code(context, command, package, source);
    }

    [Fact] void should_keep_the_command_constructor_short() => _code.ShouldContain("return new EventSourceId(");
    [Fact] void should_qualify_the_allocated_runtime_identity() => _code.ShouldContain("new sample.runtime.EventSourceId(GenerateValue.uuid().toString())");
    [Fact] void should_qualify_the_identity_component_type() => _code.ShouldContain("sample.runtime.EventSourceId eventSourceId");
    [Fact] void should_not_import_the_runtime_type_over_the_command() => _code.ShouldNotContain("import sample.runtime.EventSourceId;");
    [Fact] void should_not_allocate_the_command_as_its_identity() => _code.ShouldNotContain("new EventSourceId(GenerateValue.uuid().toString())");
}
