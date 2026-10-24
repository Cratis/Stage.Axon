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

public class when_selecting_cascaded_validation : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string date;
    string text;
    string enumeration;
    CommandJavaTypes types;

    void Because()
    {
        types = new(request.Model.Application);
        date = Render(SemanticTypeReference.ForPrimitive(SemanticPrimitiveType.Date));
        text = Render(SemanticTypeReference.ForPrimitive(SemanticPrimitiveType.Text));
        enumeration = Render(SemanticTypeReference.ForConcept(request.Model.Application.Concepts.First(concept => !concept.Values.IsDefaultOrEmpty).Id));
    }

    [Fact] void should_not_cascade_into_dates() => date.ShouldNotContain("@jakarta.validation.Valid ");
    [Fact] void should_not_cascade_into_scalars() => text.ShouldNotContain("@jakarta.validation.Valid ");
    [Fact] void should_not_cascade_into_enums() => enumeration.ShouldNotContain("@jakarta.validation.Valid ");
    [Fact] void should_retain_required_validation() => date.ShouldContain("@jakarta.validation.constraints.NotNull java.time.LocalDate value");
    [Fact] void should_cascade_into_composites() => types.Cascades(new(SemanticTypeReferenceKind.CompositeType, SemanticPrimitiveType.Text, default, false, false)).ShouldBeTrue();

    string Render(SemanticTypeReference type)
    {
        var located = ScopeSelection.Select(request).First(slice => !slice.Slice.Commands.IsEmpty);
        var seed = located.Slice.Commands[0];
        var command = seed with { Properties = [seed.Properties[0] with { Name = "value", Type = type }], Validations = [], Requirements = [] };
        var context = new AxonEmissionContext(request, new(request.Profile));
        CommandContractsEmitter.EmitCommand(context, located, command, null);
        return Encoding.UTF8.GetString(context.Artifacts[0].Bytes.AsSpan());
    }
}
