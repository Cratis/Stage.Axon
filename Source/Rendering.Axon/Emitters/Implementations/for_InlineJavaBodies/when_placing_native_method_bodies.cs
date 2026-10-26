// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter.given;
using Cratis.Stage.Rendering.Axon.Emitters.Queries;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Implementations.for_InlineJavaBodies;

public class when_placing_native_method_bodies : a_view_request
{
    const string Body = "// java.time and \\\\ must stay literal\nvar block = \"\"\"\n  authored indentation\n    java.time\n  \"\"\";\nreturn;";

    [Fact]
    void should_replace_synthetic_command_effects_after_validation()
    {
        var located = ScopeSelection.Select(_request).First(slice => slice.Slice.Commands.Any(command => !command.Produces.IsEmpty));
        var command = located.Slice.Commands.First(command => !command.Produces.IsEmpty) with { Response = null };
        var context = Context(SemanticImplementationRole.CommandHandler, command.Id, Body);
        command = command with { Handler = new(context.Request.ImplementationRequirements[0].RequirementId, 1) };
        var source = CommandSource.Create(context, command, CommandAppends.Contract(context, command.Produces[0].EventContract));
        new CommandHandlerEmitter(context, located, command, source).Emit();
        var code = Text(context.Artifacts.Single());
        code.ShouldContain(Body);
        code.ShouldContain("rules.validate(command);");
        Assert.True(code.IndexOf("rules.validate(command);", StringComparison.Ordinal) < code.IndexOf(Body, StringComparison.Ordinal));
        code.ShouldNotContain("appender.append(");
        code.ShouldNotContain("UnsupportedOperationException");
        context.ConsumedImplementations.ShouldContain(command.Handler.RequirementId);
    }

    [Fact]
    void should_fill_the_existing_query_performer_signature()
    {
        const string body = "return java.util.List.of();";
        var seed = _request.ImplementationRequirements.Single(requirement => requirement.Role == SemanticImplementationRole.QueryPerformer);
        var context = Context(seed.Role, seed.Source.SemanticId, body);
        var located = ScopeSelection.Select(_request).First(slice => slice.Slice.ReadModels.Any(model => model.Id == seed.QueryShape.ReadModel));
        var query = QueryPerformer.Queries(context, located).Single();
        QueryPerformer.Emit(context, "sample.queries", query, "sample.queries.Item");
        var code = Text(context.Artifacts[0]);
        code.ShouldContain(body);
        code.ShouldContain("perform(");
        code.ShouldContain("ReadModelStore<Object, Item> store)");
        code.ShouldNotContain("UnsupportedOperationException");
        context.ConsumedImplementations.ShouldContain(seed.RequirementId);
    }

    [Theory]
    [InlineData("csharp", false)]
    [InlineData("csharp", true)]
    [InlineData(null, false)]
    [InlineData(null, true)]
    public void should_not_place_a_foreign_or_missing_command_body(string? language, bool strict)
    {
        var located = ScopeSelection.Select(_request).First(slice => !slice.Slice.Commands.IsEmpty);
        var command = located.Slice.Commands[0] with { Response = null };
        var seed = Context(SemanticImplementationRole.CommandHandler, command.Id, Body);
        var requirement = seed.Request.ImplementationRequirements[0] with { Language = language };
        var request = seed.Request with { Profile = AxonRendering.CreateProfile(strict: strict), ImplementationRequirements = [requirement] };
        if (language is null) request = request with { ImplementationContents = request.ImplementationContents.Clear() };
        var context = new AxonEmissionContext(request, new(request.Profile));
        command = command with { Handler = new(requirement.RequirementId, 1) };
        new CommandHandlerEmitter(context, located, command, null).Emit();
        var code = Text(context.Artifacts.Single());
        code.ShouldNotContain("authored indentation");
        code.ShouldContain("throw new UnsupportedOperationException");
        context.ConsumedImplementations.ShouldBeEmpty();
        context.Degradation.UnsupportedConstructs.Any(diagnostic => diagnostic.Artifact == command.Id).ShouldBeTrue();
        context.Degradation.Refusals.IsEmpty.ShouldEqual(!strict);
    }

    [Fact]
    void should_preserve_the_declared_command_return_type_without_a_second_return()
    {
        var located = ScopeSelection.Select(_request).First(slice => !slice.Slice.Commands.IsEmpty);
        var command = located.Slice.Commands[0];
        var property = command.Properties[0];
        const string body = "return 42L;";
        var context = Context(SemanticImplementationRole.CommandHandler, command.Id, body);
        command = command with { Handler = new(context.Request.ImplementationRequirements[0].RequirementId, 1), Response = new SemanticScalarCommandResponse(property.Id, SemanticTypeReference.ForPrimitive(SemanticPrimitiveType.WholeNumber)) };
        new CommandHandlerEmitter(context, located, command, null).Emit();
        var code = Text(context.Artifacts.Single());
        code.ShouldContain("public Long handle(");
        code.ShouldContain(body + "\n    }");
        code.Split(body, StringSplitOptions.None).Length.ShouldEqual(2);
        code.ShouldNotContain("appender.append(");
    }

    AxonEmissionContext Context(SemanticImplementationRole role, SemanticId owner, string body)
    {
        var seed = _request.ImplementationRequirements.First(requirement => requirement.Role == SemanticImplementationRole.QueryPerformer);
        var address = seed.Owner;
        if (role == SemanticImplementationRole.CommandHandler)
        {
            var located = ScopeSelection.Select(_request).Single(slice => slice.Slice.Commands.Any(command => command.Id == owner));
            var command = located.Slice.Commands.Single(command => command.Id == owner);
            var sliceAddress = SemanticAddress.ForSlice(ApplicationIdentity.Create(_request.Model.Application.Name), located.Module.Name, [.. located.Features.Select(feature => feature.Name)], located.Slice.Name);
            address = SemanticAddress.ForCommand(sliceAddress, command.Name);
        }
        var requirement = seed with
        {
            Role = role,
            Owner = address,
            Source = seed.Source with { SemanticId = owner },
            Language = "java",
            ContentHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body)))
        };
        var request = _request with { ImplementationRequirements = [requirement], ImplementationContents = _request.ImplementationContents.SetItem(requirement.RequirementId, body) };
        return new(request, new(request.Profile));
    }
}
