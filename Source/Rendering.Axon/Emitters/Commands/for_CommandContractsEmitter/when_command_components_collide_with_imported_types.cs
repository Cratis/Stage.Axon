// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands.for_CommandContractsEmitter;

public class when_command_components_collide_with_imported_types : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string code;

    void Because()
    {
        var located = ScopeSelection.Select(request).First(slice => !slice.Slice.Commands.IsEmpty);
        var seed = located.Slice.Commands[0];
        var concept = request.Model.Application.Concepts[0];
        var application = request.Model.Application with
        {
            Concepts = [.. request.Model.Application.Concepts.Select(candidate => candidate.Id == concept.Id ? candidate with { Name = "NotNull" } : candidate)]
        };
        request = request with { Model = ExecutableSemanticModel.Create(request.Model.LanguageVersion, request.Model.SemanticVersion, application) };
        var property = seed.Properties[0] with { Name = "values", Type = SemanticTypeReference.ForConcept(concept.Id) with { IsCollection = true }, IsGenerated = false };
        var command = seed with { Properties = [property] };
        var context = new AxonEmissionContext(request, new(request.Profile));
        CommandContractsEmitter.EmitCommand(context, located, command, null);
        code = Encoding.UTF8.GetString(context.Artifacts[0].Bytes.AsSpan());
    }

    [Fact] void should_preclaim_the_application_type() => code.ShouldContain("import sample.concepts.NotNull;");
    [Fact] void should_qualify_only_the_colliding_constraint() => code.ShouldContain("@jakarta.validation.constraints.NotNull");
    [Fact] void should_keep_the_component_type_short() => code.ShouldContain("List<NotNull> values");
    [Fact] void should_import_the_collection_type() => code.ShouldContain("import java.util.List;");
    [Fact] void should_not_import_the_colliding_constraint() => code.ShouldNotContain("import jakarta.validation.constraints.NotNull;");
}
