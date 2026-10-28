// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Emitters.Implementations;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter;

public class when_a_native_handler_produces_a_uniquely_constrained_event : when_a_native_handler_is_constrained
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void should_enforce_every_append_in_a_private_body(bool strict) => AssertEnforced(strict);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void should_emit_a_complete_throwing_stub_and_audit_when_admission_fails(bool strict)
    {
        var scoped = request with
        {
            Profile = AxonRendering.CreateProfile(strict: strict),
            ImplementationRequirements = [request.ImplementationRequirements[0] with { ContentHash = "stale" }]
        };
        var context = new AxonEmissionContext(scoped, new(scoped.Profile));
        InlineJavaBodies.TryResolve(context, new(requirementId, SemanticImplementationRole.CommandHandler, command.Id), out _, out var reason).ShouldBeFalse();
        var source = CommandSource.Create(context, command, CommandAppends.Contract(context, command.Produces[0].EventContract));
        new CommandHandlerEmitter(context, located, command, source).Emit();
        InlineImplementationDiagnostics.Emit(context);
        var code = Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + command.Name + "Handler.java", StringComparison.Ordinal)));
        var message = AxonRefusalCodes.Implementation + ": execute the attached command implementation: " + reason;
        code.ShouldContain("throw new UnsupportedOperationException(" + JavaCodeBuilder.StringLiteral(message) + ");\n    }");
        code.ShouldNotContain("native-constraint-body");
        code.ShouldNotContain("rules.validate(command);");
        context.ConsumedImplementations.ShouldBeEmpty();
        context.Degradation.Refusals.IsEmpty.ShouldEqual(!strict);
        Text(context.Artifacts[^1]).ShouldContain("SHA-256");
    }
}
