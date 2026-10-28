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
    string clockFixture;
    SemanticCommand generatedCommand;
    SemanticCommand occurredCommand;

    void Because()
    {
        var located = ScopeSelection.Select(request).First(slice => !slice.Slice.Commands.IsEmpty);
        var seed = located.Slice.Commands[0];
        ordinary = Render(located, seed with { Authorization = null }, out fixture);
        generatedCommand = seed with { Properties = [.. seed.Properties.Select(property => property.IsIdentifier ? property with { IsGenerated = true } : property)] };
        generated = Render(located, generatedCommand, out clockFixture);
        var application = request.Model.Application;
        var contract = CommandAppends.Contract(new(request, new(request.Profile)), seed.Produces[0].EventContract);
        var target = contract.Properties[0] with { Id = SemanticId.Create(SemanticKind.Property, "occurred"), Name = "occurred", IsIdentifier = false, Type = SemanticTypeReference.ForPrimitive(SemanticPrimitiveType.DateTime) with { IsOptional = true } };
        var changed = contract with { Properties = [.. contract.Properties, target] };
        var production = seed.Produces[0] with { Mappings = [.. seed.Produces[0].Mappings, new(target.Id, new SemanticEventContextExpression(SemanticEventContextValueKind.Occurred, SemanticTypeReference.ForPrimitive(SemanticPrimitiveType.DateTime)))] };
        var command = seed with { Produces = [production] };
        occurredCommand = command;
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
    [Fact] void should_construct_clock_free_handlers_in_fixtures() => fixture.ShouldContain("new PlaceOrderHandler()");
    [Fact] void should_omit_unused_fixture_clocks() => fixture.ShouldNotContain("java.time.Clock");
    [Fact] void should_keep_the_clock_for_generation() => generated.ShouldContain("command = command.withGenerated(clock);");
    [Fact] void should_load_the_allocated_identity_through_the_state_manager() => generated.ShouldContain("loadManagedEntity(PlaceOrderStream.class, command.orderId(), context).join();");
    [Fact] void should_keep_the_processing_context_for_generation() => generated.ShouldContain("ProcessingContext context");
    [Fact] void should_import_the_processing_context() => generated.ShouldContain("import org.axonframework.messaging.core.unitofwork.ProcessingContext;");
    [Fact] void should_import_the_state_manager() => generated.ShouldContain("import org.axonframework.modelling.StateManager;");
    [Fact] void should_use_the_short_state_manager_name() => generated.ShouldContain("context.component(StateManager.class)");
    [Fact] void should_not_inject_state_before_allocating_an_identity() => generated.ShouldNotContain("@InjectEntity");
    [Fact] void should_not_create_an_occurrence_for_generation_alone() => generated.ShouldNotContain("var occurred =");
    [Fact] void should_keep_the_clock_for_occurrence_mapping() => occurred.ShouldContain("private final Clock clock;");
    [Fact] void should_import_the_clock() => occurred.ShouldContain("import java.time.Clock;");
    [Fact] void should_import_the_occurrence_type() => occurred.ShouldContain("import java.time.OffsetDateTime;");
    [Fact] void should_import_the_utc_offset() => occurred.ShouldContain("import java.time.ZoneOffset;");
    [Fact] void should_read_the_clock_for_occurrence_mapping() => occurred.ShouldContain("var occurred = OffsetDateTime.ofInstant(clock.instant(), ZoneOffset.UTC);");
    [Fact] void should_not_import_unused_authorization() => ordinary.ShouldNotContain("import org.springframework.security.access.prepost.PreAuthorize;");
    [Fact] void should_use_short_fixture_clock_names() => clockFixture.ShouldContain("handler(Clock clock)");
    [Fact] void should_import_the_fixture_clock() => clockFixture.ShouldContain("import java.time.Clock;");
    [Fact] void should_use_short_fixture_clock_expressions() => clockFixture.ShouldContain("Clock.fixed(OffsetDateTime.parse(instant).toInstant(), ZoneOffset.UTC)");
    [Fact] void should_use_short_fixture_identity_literals() => fixture.ShouldContain("UUID.fromString(");
    [Fact] void should_import_the_fixture_identity_type() => fixture.ShouldContain("import java.util.UUID;");

    [Theory]
    [InlineData("Clock", "java.time.Clock", "private final java.time.Clock clock;")]
    [InlineData("ProcessingContext", "org.axonframework.messaging.core.unitofwork.ProcessingContext", "ProcessingContext command, EventAppender appender, org.axonframework.messaging.core.unitofwork.ProcessingContext context")]
    [InlineData("StateManager", "org.axonframework.modelling.StateManager", "context.component(org.axonframework.modelling.StateManager.class)")]
    [InlineData("Component", "org.springframework.stereotype.Component", "@org.springframework.stereotype.Component")]
    [InlineData("CommandHandler", "org.axonframework.messaging.commandhandling.annotation.CommandHandler", "@org.axonframework.messaging.commandhandling.annotation.CommandHandler")]
    [InlineData("EventAppender", "org.axonframework.messaging.eventhandling.gateway.EventAppender", "EventAppender command, org.axonframework.messaging.eventhandling.gateway.EventAppender appender")]
    [InlineData("OffsetDateTime", "java.time.OffsetDateTime", "java.time.OffsetDateTime.ofInstant(clock.instant(), ZoneOffset.UTC)")]
    [InlineData("ZoneOffset", "java.time.ZoneOffset", "OffsetDateTime.ofInstant(clock.instant(), java.time.ZoneOffset.UTC)")]
    public void should_keep_the_declared_command_name_on_a_framework_collision(string name, string frameworkType, string expected)
    {
        var input = name.Equals("OffsetDateTime", StringComparison.Ordinal) || name.Equals("ZoneOffset", StringComparison.Ordinal) ? occurredCommand : generatedCommand;
        var command = input with { Name = name };
        var context = new AxonEmissionContext(request, new(request.Profile));
        var located = ScopeSelection.Select(request).First(slice => !slice.Slice.Commands.IsEmpty);
        var source = CommandSource.Create(context, command, CommandAppends.Contract(context, command.Produces[0].EventContract));
        new CommandHandlerEmitter(context, located, command, source).Emit();
        var output = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith(name + "Handler.java", StringComparison.Ordinal)).Bytes.AsSpan());
        output.ShouldContain(expected);
        output.ShouldNotContain("import " + frameworkType + ";");
    }

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
