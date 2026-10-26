// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_ReadModelRecords;

public class when_a_model_is_named_uuid : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string collision;
    string regular;

    void Because()
    {
        var types = new ViewJavaTypes(request.Model.Application);
        var model = ScopeSelection.Select(request).SelectMany(slice => slice.Slice.ReadModels).First();
        var property = model.Properties[0] with { Name = "identity", Type = new(SemanticTypeReferenceKind.Primitive, SemanticPrimitiveType.Uuid, default, false, false) };
        collision = ReadModelRecords.Code(types, "example.views", model with { Name = "UUID", Properties = [property] });
        regular = ReadModelRecords.Code(types, "example.views", model with { Name = "RegularView", Properties = [property] });
    }

    [Fact] void should_keep_the_application_model_short() => collision.ShouldContain("public record UUID(java.util.UUID identity)");
    [Fact] void should_qualify_the_colliding_copy_parameter() => collision.ShouldContain("public UUID withIdentity(java.util.UUID value)");
    [Fact] void should_not_import_the_colliding_framework_type() => collision.ShouldNotContain("import java.util.UUID;");

    [Fact]
    void should_not_inline_framework_types_in_a_noncolliding_model()
    {
        var body = string.Join('\n', regular.Split('\n').Where(line => !line.TrimStart().StartsWith("import ", StringComparison.Ordinal)));
        new[] { "java.", "org.axonframework.", "org.springframework.", "jakarta.", "io.cratis." }
            .Any(prefix => body.Contains(prefix, StringComparison.Ordinal)).ShouldBeFalse();
    }
}
