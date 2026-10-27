// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Admission.for_AxonBehaviorAdmission;

public class when_a_routed_native_command_is_planned : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    const string Routing = "STAGE-AXON-ROUTING";

    SemanticCommand command;
    ArtifactRenderRequest routed;
    ArtifactRenderRequest unrouted;

    void Establish()
    {
        var located = ScopeSelection.Select(request).First(candidate => candidate.Slice.Commands.Any(item => item.Produces.Length > 0));
        command = located.Slice.Commands.First(item => item.Produces.Length > 0);
        const string body = "/* native */";
        var requirementId = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{command.Id}|CommandHandler|0:")));
        var sliceAddress = SemanticAddress.ForSlice(ApplicationIdentity.Create(request.Model.Application.Name), located.Module.Name, [.. located.Features.Select(feature => feature.Name)], located.Slice.Name);
        var requirement = new SemanticImplementationRequirement(SemanticImplementationRole.CommandHandler, SemanticAddress.ForCommand(sliceAddress, command.Name), null, "java", null, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body))), new(command.Id, default, default)) { RequirementId = requirementId, RequiredCapability = "provider-defined" };
        var stream = new SemanticEventStream(SemanticId.Create(SemanticKind.EventStream, "Archive"), "Archive", "stream") { StreamIdType = new SemanticTypeReference(SemanticTypeReferenceKind.Primitive, SemanticPrimitiveType.Text, default, false, false), StreamIdParts = [] };
        var source = new SemanticEventSource(SemanticId.Create(SemanticKind.EventSource, "Orders"), "Orders", "aggregate", [stream]) { IdentifierType = command.Produces[0].DestinationType };
        routed = Build(command with { Handler = new(requirementId, 1), Route = new(source.Id, stream.Id) { StreamId = new SemanticValueExpression(new SemanticTextValue("archive")) } });
        unrouted = Build(command with { Handler = new(requirementId, 1) });

        ArtifactRenderRequest Build(SemanticCommand changed)
        {
            SemanticFeature Feature(SemanticFeature feature) => feature with { Features = [.. feature.Features.Select(Feature)], Slices = [.. feature.Slices.Select(slice => slice with { Commands = [.. slice.Commands.Select(item => item.Id == command.Id ? changed : item)] })] };
            var application = request.Model.Application with { Modules = [.. request.Model.Application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })], EventSources = [source] };
            return request with { Model = ExecutableSemanticModel.Create(new(23, 0), SemanticVersion.V23, application), ImplementationRequirements = [.. request.ImplementationRequirements, requirement], ImplementationContents = request.ImplementationContents.SetItem(requirementId, body) };
        }
    }

    [Fact]
    void should_degrade_the_generated_command_specs_only_when_routed()
    {
        var plain = EmitAll(unrouted);
        var degraded = EmitAll(routed);
        plain.Degradation.UnsupportedConstructs.Any(diagnostic => diagnostic.Code == Routing).ShouldBeFalse();
        plain.Degradation.UnsupportedConstructs.Any(diagnostic => diagnostic.Code == AxonRefusalCodes.CommandSpecification).ShouldBeFalse();
        degraded.Degradation.UnsupportedConstructs.Count(diagnostic => diagnostic.Code == Routing && diagnostic.Artifact == command.Id).ShouldEqual(1);
        degraded.Degradation.UnsupportedConstructs.Any(diagnostic => diagnostic.Code == AxonRefusalCodes.CommandSpecification).ShouldBeTrue();
        degraded.Specifications.Values.Any(specification => specification.DisabledCode == AxonRefusalCodes.CommandSpecification).ShouldBeTrue();
    }

    [Fact]
    void should_refuse_the_routing_in_strict_mode() =>
        EmitAll(routed with { Profile = AxonRendering.CreateProfile(strict: true) }).Degradation.Refusals.Any(refusal => refusal.Codes.Contains(Routing)).ShouldBeTrue();

    static AxonEmissionContext EmitAll(ArtifactRenderRequest request)
    {
        var context = new AxonEmissionContext(request, new(request.Profile));
        foreach (var emitter in AxonEmitters.Ordered.Where(emitter => emitter.IsImplemented)) emitter.Emit(context);
        return context;
    }
}
