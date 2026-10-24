// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Admission;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Concepts.for_AxonConceptsEmitter;

public class when_emitting_concepts : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext context;

    void Because()
    {
        context = new(request, new AxonDegradation(request.Profile));
        new AxonConceptsEmitter().Emit(context);
    }

    [Fact]
    void should_emit_scalar_concepts_as_canonical_records()
    {
        var id = Content("OrderId");
        id.ShouldContain("public record OrderId(java.util.UUID value) implements " + context.BasePackage + ".runtime.ConceptAs<java.util.UUID>");
        id.ShouldContain("java.util.Objects.requireNonNull(value, \"value\")");
        id.ShouldContain("public String toString() { return value.toString(); }");
    }

    [Fact]
    void should_preserve_string_enumeration_values()
    {
        var status = Content("OrderStatus");
        status.ShouldContain("public enum OrderStatus");
        status.ShouldContain("NEW_(\"new\")");
        status.ShouldContain("@JsonCreator");
        status.ShouldContain("@JsonValue");
        status.ShouldContain("public String value()");
        status.ShouldContain("public String toString() { return value.toString(); }");
    }

    [Fact]
    void should_keep_decimal_concepts_exact() => Content("Money").ShouldContain("java.math.BigDecimal value");

    [Fact]
    void should_attach_the_model_identity_to_every_concept() =>
        context.Artifacts.TrueForAll(artifact => artifact.Sources.Length == 1).ShouldBeTrue();

    string Content(string type) => Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + type + ".java", StringComparison.Ordinal)).Bytes.AsSpan());
}
