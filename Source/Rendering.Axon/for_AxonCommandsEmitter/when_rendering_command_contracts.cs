// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter;

public class when_rendering_command_contracts : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext context;
    string contract;

    void Establish()
    {
        var selected = ScopeSelection.Select(request).First(slice => !slice.Slice.Commands.IsEmpty);
        request = request with { Scope = new(ArtifactRenderScopeKind.Slice, selected.Slice.Id) };
        context = new(request, new(request.Profile));
    }

    void Because()
    {
        new CommandContractsEmitter().Emit(context);
        contract = Encoding.UTF8.GetString(context.Artifacts.First(artifact => !artifact.RelativePath.EndsWith("Rules.java", StringComparison.Ordinal)).Bytes.AsSpan());
    }

    [Fact] void should_emit_a_record() => contract.ShouldContain("public record ");
    [Fact] void should_validate_nested_concepts() => contract.ShouldContain("@jakarta.validation.Valid ");
    [Fact] void should_validate_required_components() => contract.ShouldContain("@jakarta.validation.constraints.NotNull ");
    [Fact] void should_map_the_java_uuid_concept() => contract.ShouldContain(".concepts.OrderId orderId");
    [Fact] void should_emit_only_selected_commands() => context.Artifacts.Count.ShouldEqual(ScopeSelection.Select(request)[0].Slice.Commands.Length * 2);
    [Fact] void should_trace_the_command_sources() => context.Artifacts.TrueForAll(artifact => artifact.Sources.Any(id => ScopeSelection.Select(request)[0].Slice.Commands.Any(command => command.Id == id))).ShouldBeTrue();
}
