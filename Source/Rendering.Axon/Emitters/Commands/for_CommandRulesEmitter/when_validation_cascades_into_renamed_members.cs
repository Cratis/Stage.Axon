// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Semantics;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands.for_CommandRulesEmitter;

public class when_validation_cascades_into_renamed_members : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string code;

    async Task Because()
    {
        var views = await SemanticModelLoader.LoadFromPathAsync(Path.Combine(for_AxonArtifactRenderPlanner.given.blueprint_models.Root(), "Corpus", "axon-samples", "views"), null, "AxonViews");
        var address = views.Model.Application.Types.Single(type => type.Name == "Card");
        var other = views.Model.Application.Types.Single(type => type.Name == "BoardDetails");
        var seed = ScopeSelection.Select(request).First(slice => !slice.Slice.Commands.IsEmpty).Slice.Commands[0];
        var property = address.Properties[0] with { Type = SemanticTypeReference.ForPrimitive(SemanticPrimitiveType.Text) };
        var nested = address.Properties[1] with { Name = "child", Type = new(SemanticTypeReferenceKind.CompositeType, SemanticPrimitiveType.Unknown, address.Id, false, false) { IsCollection = true } };
        var application = request.Model.Application with
        {
            Types = [address with { Properties = [property with { Name = "class" }, nested] }, other with { Properties = [property with { Id = other.Properties[0].Id, Name = "Class" }] }]
        };
        var command = seed with
        {
            Properties = [nested with { Name = "address" }, nested with { Name = "other", Type = nested.Type with { Target = other.Id } }],
            Validations = [],
            Requirements = []
        };
        var model = ExecutableSemanticModel.Create(request.Model.LanguageVersion, request.Model.SemanticVersion, application);
        var context = new AxonEmissionContext(request with { Model = model }, new(request.Profile));
        code = new CommandRulesEmitter(context, command, new(application, context.BasePackage)).Render("example.commands");
    }

    [Fact] void should_translate_even_when_only_a_nested_name_differs() => code.ShouldContain("validation.reject(validationMember(violation.getPropertyPath().toString()), violation.getMessage())");
    [Fact] void should_follow_the_cascaded_property_type() => code.ShouldContain("case \"0:address\" -> 1;");
    [Fact] void should_translate_the_nested_member() => code.ShouldContain("case \"1:class_\" -> \"class\";");
    [Fact] void should_distinguish_the_same_java_member_in_another_type() => code.ShouldContain("case \"2:class_\" -> \"Class\";");
    [Fact] void should_handle_recursive_composite_types() => code.ShouldContain("case \"1:child\" -> 1;");
    [Fact] void should_translate_each_path_segment() => code.ShouldContain("for (var segment : path.split(\"\\\\.\", -1))");
    [Fact] void should_keep_collection_indices() => code.ShouldContain("var suffix = bracket < 0 ? \"\" : segment.substring(bracket);");
    [Fact] void should_keep_path_separators() => code.ShouldContain("if (!result.isEmpty()) result.append('.');");
    [Fact] void should_append_the_translated_member_with_its_index() => code.ShouldContain("result.append(member).append(suffix);");
}
