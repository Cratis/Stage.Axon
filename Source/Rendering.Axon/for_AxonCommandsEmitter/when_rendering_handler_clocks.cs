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

public class when_rendering_handler_clocks : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string ordinary;
    string generated;
    string occurred;
    string fixture;

    void Because()
    {
        var located = ScopeSelection.Select(request).First(slice => !slice.Slice.Commands.IsEmpty);
        var seed = located.Slice.Commands[0];
        ordinary = Render(located, seed, out fixture);
        generated = Render(located, seed with { Properties = [.. seed.Properties.Select(property => property.IsIdentifier ? property with { IsGenerated = true } : property)] }, out _);
        var application = request.Model.Application;
        var contract = CommandAppends.Contract(new(request, new(request.Profile)), seed.Produces[0].EventContract);
        var target = contract.Properties[0] with { Id = SemanticId.Create(SemanticKind.Property, "occurred"), Name = "occurred", IsIdentifier = false, Type = SemanticTypeReference.ForPrimitive(SemanticPrimitiveType.DateTime) with { IsOptional = true } };
        var changed = contract with { Properties = [.. contract.Properties, target] };
        var production = seed.Produces[0] with { Mappings = [.. seed.Produces[0].Mappings, new(target.Id, new SemanticEventContextExpression(SemanticEventContextValueKind.Occurred, SemanticTypeReference.ForPrimitive(SemanticPrimitiveType.DateTime)))] };
        var command = seed with { Produces = [production] };
        application = application with { Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })] };
        request = request with { Model = ExecutableSemanticModel.Create(request.Model.LanguageVersion, request.Model.SemanticVersion, application) };
        occurred = Render(located, command, out _);

        SemanticFeature Feature(SemanticFeature feature) => feature with
        {
            Features = [.. feature.Features.Select(Feature)],
            Slices = [.. feature.Slices.Select(slice => slice with
            {
                Events = [.. slice.Events.Select(candidate => candidate.Id == contract.Id ? changed : candidate)],
                Specifications = [],
                Commands = [.. slice.Commands.Select(candidate => candidate.Id == command.Id ? command : candidate)]
            })]
        };
    }

    [Fact] void should_omit_an_unused_occurrence_local() => ordinary.ShouldNotContain("var occurred =");
    [Fact] void should_omit_an_unused_clock_dependency() => ordinary.ShouldNotContain("java.time.Clock");
    [Fact] void should_construct_clock_free_handlers_in_fixtures() => fixture.ShouldContain("new PlaceOrderHandler(new PlaceOrderRules(validators.getValidator()))");
    [Fact] void should_omit_unused_fixture_clocks() => fixture.ShouldNotContain("java.time.Clock");
    [Fact] void should_keep_the_clock_for_generation() => generated.ShouldContain("command = command.withGenerated(clock);");
    [Fact] void should_not_create_an_occurrence_for_generation_alone() => generated.ShouldNotContain("var occurred =");
    [Fact] void should_keep_the_clock_for_occurrence_mapping() => occurred.ShouldContain("private final java.time.Clock clock;");
    [Fact] void should_read_the_clock_for_occurrence_mapping() => occurred.ShouldContain("var occurred = java.time.OffsetDateTime.ofInstant(clock.instant(), java.time.ZoneOffset.UTC);");

    string Render(LocatedSemanticSlice located, SemanticCommand command, out string specification)
    {
        var context = new AxonEmissionContext(request, new(request.Profile));
        var source = CommandSource.Create(context, command, CommandAppends.Contract(context, command.Produces[0].EventContract));
        var usesClock = new CommandHandlerEmitter(context, located, command, source).Emit();
        new CommandSpecificationsEmitter(context, located, command, source, usesClock).Emit();
        specification = Code("SpecificationsTest");
        return Code("Handler");

        string Code(string suffix) => Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("PlaceOrder" + suffix + ".java", StringComparison.Ordinal)).Bytes.AsSpan());
    }
}
