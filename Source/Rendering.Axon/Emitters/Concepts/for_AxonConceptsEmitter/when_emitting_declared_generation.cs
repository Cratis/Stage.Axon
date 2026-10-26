// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Admission;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Concepts.for_AxonConceptsEmitter;

public class when_emitting_declared_generation : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext context;

    void Establish() => context = new(request, new AxonDegradation(request.Profile));

    [Fact]
    void should_emit_the_uuid_factory_using_fundamentals()
    {
        var template = loaded.Model.Application.Concepts.First(concept => concept.Primitive == SemanticPrimitiveType.Uuid);
        var concept = new SemanticConcept(template.Id, "GeneratedId", SemanticPrimitiveType.Uuid, [], []) { Generation = new("uuid") };
        var code = AxonConceptsEmitter.Scalar(context, concept);
        code.ShouldContain("public static GeneratedId create() { return new GeneratedId(GenerateValue.uuid()); }");
        code.ShouldContain("Generatable<GeneratedId>");
        code.ShouldContain("import io.cratis.fundamentals.concepts.GenerateValue;");
        code.ShouldContain("import io.cratis.fundamentals.concepts.Generatable;");
        context.Degradation.UnsupportedConstructs.ShouldBeEmpty();
    }

    [Fact]
    void should_preserve_explicit_numeric_enum_values()
    {
        var template = loaded.Model.Application.Concepts[0];
        var concept = new SemanticConcept(template.Id, "Priority", SemanticPrimitiveType.Unknown, ["low", "high"], []) { Numbers = [10, 30] };
        var code = ConceptEnums.Emit(context, concept);
        code.ShouldContain("LOW(10)");
        code.ShouldContain("HIGH(30)");
        code.ShouldContain("@JsonValue\n    public Integer value()");
        code.ShouldContain("@JsonCreator\n    public static Priority fromValue(Integer value)");
        code.ShouldContain("candidate.value.equals(value)");
        code.ShouldContain("throw new IllegalArgumentException(\"Unknown Priority value: \" + value)");
    }
}
