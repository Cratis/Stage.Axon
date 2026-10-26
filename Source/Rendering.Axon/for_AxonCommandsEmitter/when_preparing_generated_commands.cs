// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter;

public class when_preparing_generated_commands : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string supported;
    string unsupported;
    string allocated;
    AxonEmissionContext unsupportedContext;

    void Because()
    {
        supported = Render(SemanticPrimitiveType.Uuid, out _);
        unsupported = Render(SemanticPrimitiveType.DateTime, out unsupportedContext);
        var context = new AxonEmissionContext(request, new(request.Profile));
        var seed = ScopeSelection.Select(request).First(slice => !slice.Slice.Commands.IsEmpty).Slice.Commands[0];
        var source = new CommandSource
        {
            Primary = ("eventSourceId", context.BasePackage + ".runtime.EventSourceId", "eventSourceId"),
            Type = SemanticTypeReference.ForPrimitive(SemanticPrimitiveType.Text),
            TagReference = "EventTags.EVENT_SOURCE_ID"
        };
        var body = new JavaCodeBuilder(context.BasePackage);
        new CommandPreparation(context, seed with { Properties = [] }, source).Emit(body);
        allocated = body.Build();
    }

    [Fact] void should_allocate_identities_using_fundamentals() => allocated.ShouldContain("new EventSourceId(GenerateValue.uuid().toString())");
    [Fact] void should_import_allocated_runtime_identities() => allocated.ShouldContain("import " + JavaNames.PackageSegment(request.Model.Application.Name) + ".runtime.EventSourceId;");
    [Fact] void should_import_fundamentals_generation() => allocated.ShouldContain("import io.cratis.fundamentals.concepts.GenerateValue;");
    [Fact] void should_import_the_preparation_clock() => allocated.ShouldContain("import java.time.Clock;");
    [Fact] void should_exclude_generated_values_from_the_http_body() => supported.ShouldContain("@JsonIgnore UUID requestId");
    [Fact] void should_clear_values_before_the_http_handler_dispatches() => supported.ShouldContain("public PlaceOrder forRequest() { return new PlaceOrder(null); }");
    [Fact] void should_generate_uuid_inputs_in_the_handler_preparation() => supported.ShouldContain("requestId() == null ? UUID.randomUUID() : requestId()");
    [Fact] void should_allow_deterministic_fixture_values() => supported.ShouldContain(": requestId()");
    [Fact] void should_warn_about_unsupported_generation() => unsupportedContext.Degradation.UnsupportedConstructs.Single().Code.ShouldEqual("STAGE-AXON-GENERATION");
    [Fact] void should_explain_the_missing_generation() => unsupported.ShouldContain("// Screenplay wanted to generate the command's modeled values");
    [Fact] void should_fail_closed_instead_of_generating_an_invented_value() => unsupported.ShouldContain("throw new UnsupportedOperationException(");

    [Fact]
    void should_reserve_the_command_name_before_framework_references()
    {
        var command = Template("Clock", SemanticPrimitiveType.Uuid);
        var context = new AxonEmissionContext(request, new(request.Profile));
        var body = new JavaCodeBuilder(context.BasePackage + ".commands");
        new CommandPreparation(context, command, null).Emit(body);
        var code = body.Build();
        code.ShouldContain("withGenerated(java.time.Clock clock)");
        code.ShouldContain("UUID.randomUUID()");
        code.ShouldNotContain("import java.time.Clock;");
    }

    [Fact]
    void should_reserve_the_command_name_before_fundamentals_generation()
    {
        var command = Template("GenerateValue", SemanticPrimitiveType.Text) with { Properties = [] };
        var context = new AxonEmissionContext(request, new(request.Profile));
        var body = new JavaCodeBuilder(context.BasePackage + ".commands");
        var source = new CommandSource
        {
            Primary = ("identity", context.BasePackage + ".runtime.EventSourceId", "identity"),
            Type = SemanticTypeReference.ForPrimitive(SemanticPrimitiveType.Text),
            TagReference = "EventTags.EVENT_SOURCE_ID"
        };
        new CommandPreparation(context, command, source).Emit(body);
        var code = body.Build();
        code.ShouldContain("new EventSourceId(io.cratis.fundamentals.concepts.GenerateValue.uuid().toString())");
        code.ShouldNotContain("import io.cratis.fundamentals.concepts.GenerateValue;");
    }

    [Fact]
    void should_not_import_generators_for_an_unsupported_strategy()
    {
        unsupported.ShouldNotContain("import java.util.UUID;");
        unsupported.ShouldNotContain("import io.cratis.fundamentals.concepts.GenerateValue;");
    }

    SemanticCommand Template(string name, SemanticPrimitiveType primitive)
    {
        var command = ScopeSelection.Select(request).First(slice => !slice.Slice.Commands.IsEmpty).Slice.Commands[0];
        var property = command.Properties[0] with { Name = "requestId", Type = SemanticTypeReference.ForPrimitive(primitive), IsGenerated = true };
        return command with { Name = name, Properties = [property] };
    }

    string Render(SemanticPrimitiveType primitive, out AxonEmissionContext context)
    {
        var located = ScopeSelection.Select(request).First(slice => !slice.Slice.Commands.IsEmpty);
        var seed = located.Slice.Commands[0];
        var property = seed.Properties[0] with { Name = "requestId", Type = SemanticTypeReference.ForPrimitive(primitive), IsGenerated = true };
        var command = seed with { Properties = [property], Validations = [], Requirements = [] };
        context = new(request, new(request.Profile));
        CommandContractsEmitter.EmitCommand(context, located, command, null);
        return Encoding.UTF8.GetString(context.Artifacts[0].Bytes.AsSpan());
    }
}
