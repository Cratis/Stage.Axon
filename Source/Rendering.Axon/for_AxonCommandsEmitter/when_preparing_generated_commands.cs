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

public class when_preparing_generated_commands : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string supported;
    string unsupported;
    AxonEmissionContext unsupportedContext;

    void Because()
    {
        supported = Render(SemanticPrimitiveType.Uuid, out _);
        unsupported = Render(SemanticPrimitiveType.DateTime, out unsupportedContext);
    }

    [Fact] void should_exclude_generated_values_from_the_http_body() => supported.ShouldContain("@com.fasterxml.jackson.annotation.JsonIgnore java.util.UUID requestId");
    [Fact] void should_clear_values_before_the_http_handler_dispatches() => supported.ShouldContain("public PlaceOrder forRequest() { return new PlaceOrder(null); }");
    [Fact] void should_generate_uuid_inputs_in_the_handler_preparation() => supported.ShouldContain("requestId() == null ? java.util.UUID.randomUUID() : requestId()");
    [Fact] void should_allow_deterministic_fixture_values() => supported.ShouldContain(": requestId()");
    [Fact] void should_warn_about_unsupported_generation() => unsupportedContext.Degradation.UnsupportedConstructs.Single().Code.ShouldEqual("STAGE-AXON-GENERATION");
    [Fact] void should_explain_the_missing_generation() => unsupported.ShouldContain("// Screenplay wanted to generate the command's modeled values");
    [Fact] void should_fail_closed_instead_of_generating_an_invented_value() => unsupported.ShouldContain("throw new UnsupportedOperationException(");

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
