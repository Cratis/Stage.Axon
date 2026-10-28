// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Events;
using Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter.given;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands.for_CommandHandlerEmitter;

public class when_emitting_a_native_body_for_produced_events : a_view_request
{
    const string Body = "// authored body";

    string _code = string.Empty;
    string _eventType = string.Empty;

    void Because()
    {
        var located = ScopeSelection.Select(_request).First(slice => slice.Slice.Commands.Any(command => !command.Produces.IsEmpty));
        var command = located.Slice.Commands.First(command => !command.Produces.IsEmpty) with { Response = null };
        var sliceAddress = SemanticAddress.ForSlice(ApplicationIdentity.Create(_request.Model.Application.Name), located.Module.Name, [.. located.Features.Select(feature => feature.Name)], located.Slice.Name);
        var seed = _request.ImplementationRequirements.First(requirement => requirement.Role == SemanticImplementationRole.QueryPerformer);
        var requirement = seed with
        {
            Role = SemanticImplementationRole.CommandHandler,
            Owner = SemanticAddress.ForCommand(sliceAddress, command.Name),
            Source = seed.Source with { SemanticId = command.Id },
            Language = "java",
            ContentHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Body)))
        };
        var request = _request with { ImplementationRequirements = [requirement], ImplementationContents = _request.ImplementationContents.SetItem(requirement.RequirementId, Body) };
        var context = new AxonEmissionContext(request, new(request.Profile));
        command = command with { Handler = new(requirement.RequirementId, 1) };
        _eventType = AxonEventsEmitter.TypeFor(context, CommandAppends.Contract(context, command.Produces[0].EventContract).Id);
        new CommandHandlerEmitter(context, located, command, CommandSource.Create(context, command, CommandAppends.Contract(context, command.Produces[0].EventContract))).Emit();
        _code = Encoding.UTF8.GetString(context.Artifacts.Single().Bytes.AsSpan());
    }

    [Fact]
    void should_place_the_authored_body() => _code.ShouldContain(Body);

    [Fact]
    void should_import_the_produced_event_so_the_body_can_use_its_simple_name() =>
        Assert.True(_code.Contains($"import {_eventType};", StringComparison.Ordinal) || _eventType[.._eventType.LastIndexOf('.')] == PackageOf(_code));

    static string PackageOf(string code) => code[(code.IndexOf("package ", StringComparison.Ordinal) + 8)..code.IndexOf(';', StringComparison.Ordinal)];
}
