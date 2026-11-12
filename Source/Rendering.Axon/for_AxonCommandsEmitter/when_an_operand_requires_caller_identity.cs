// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter;

public class when_an_operand_requires_caller_identity : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    [Fact]
    void should_not_treat_the_identity_as_a_constant_or_property()
    {
        var command = ScopeSelection.Select(request).SelectMany(located => located.Slice.Commands).First();
        var property = command.Properties[0] with { Type = new(SemanticTypeReferenceKind.Primitive, SemanticPrimitiveType.Text, default, false, false) };
        command = command with { Properties = [property] };
        var types = new CommandJavaTypes(request.Model.Application, "example");
        var operand = new SemanticConditionOperand(property.Id, new SemanticTextValue("forged identity")) { CallerId = true };
        var comparison = new SemanticComparison(new(property.Id, null), SemanticComparisonOperator.Equal, operand);
        new CommandConditions(command, types).Render(comparison).ShouldBeNull();
    }
}
