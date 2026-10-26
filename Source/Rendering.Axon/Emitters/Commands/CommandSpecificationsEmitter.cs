// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters.Specifications;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands;

internal sealed class CommandSpecificationsEmitter(AxonEmissionContext context, LocatedSemanticSlice located, SemanticCommand command, CommandSource? source, bool usesClock)
{
    internal void Emit()
    {
        var specifications = located.Slice.Specifications.Where(specification => specification.When?.Command == command.Id).ToArray();
        if (specifications.Length == 0) return;
        var package = CommandContractsEmitter.Package(new(context.Request.Model.Application), located);
        var name = JavaNames.Type(command.Name) + "SpecificationsTest";
        var commandName = JavaNames.Type(command.Name);
        var code = new JavaCodeBuilder(package).Declare(name).Declare(commandName).Declare(commandName + "Rules").Declare(commandName + "Handler");
        ClaimSource(code, commandName);
        ClaimAssertions(code, specifications);
        code.Block("class " + name, body =>
        {
            Setup(body);
            for (var index = 0; index < specifications.Length; index++) Specification(body, specifications[index], package + "." + name, index);
        });
        context.AddJava(package, name, code.Build(), test: true, sources: [command.Id, .. specifications.Select(specification => specification.Id)]);
    }

    static bool MissingValues(string? input, string?[] given, string?[] expected) => input is null || given.Any(value => value is null) || expected.Any(value => value is null);

    static bool HasReadModelFixtures(SemanticSpecification specification) =>
        !specification.GivenReadModels.IsEmpty || !specification.ThenReadModels.IsEmpty || !specification.ThenQueries.IsEmpty || !specification.ThenAbsentReadModels.IsEmpty;

    void ClaimSource(JavaCodeBuilder code, string commandName)
    {
        if (source is null) return;
        code.Declare(commandName + "Stream");
        if (source.JavaType.StartsWith(context.BasePackage + ".", StringComparison.Ordinal)) code.Import(source.JavaType);
    }

    void ClaimAssertions(JavaCodeBuilder code, IEnumerable<SemanticSpecification> specifications)
    {
        foreach (var specification in specifications)
        {
            var values = new CommandFixtureValues(context, command, specification, source);
            if (Unsupported(specification, values) is not null) continue;
            if (MissingValues(values.Command(), [.. specification.GivenEvents.Select(values.Event)], [.. specification.ThenEvents.Select(values.Event)])) continue;
            new CommandFixtureAssertions(context, command, specification).Claim(code);
        }
    }

    void Setup(JavaCodeBuilder body)
    {
        var name = JavaNames.Type(command.Name);
        body.Line($"private final {body.Reference("jakarta.validation.ValidatorFactory")} validators = {body.Reference("jakarta.validation.Validation")}.buildDefaultValidatorFactory();")
            .Line($"private {body.Reference("org.axonframework.test.fixture.AxonTestFixture")} fixture;").Line($"@{body.Reference("org.junit.jupiter.api.AfterEach")}").Block("void stop()", method => method.Line("if (fixture != null) fixture.stop();")
                .Line("validators.close();").Line($"{method.Reference("org.springframework.security.core.context.SecurityContextHolder")}.clearContext();"));
        CommandFixtureSecurity.ProxyReferences(body, name, usesClock);
        body.Block($"private {body.Reference("org.axonframework.test.fixture.AxonTestFixture")} fixture({body.Reference("java.lang.String")} instant)", method =>
        {
            FixtureClock(method);
            method.Line($"var configuration = {method.Reference("org.axonframework.eventsourcing.configuration.EventSourcingConfigurer")}.create();");
            if (source is not null) method.Line($"configuration.registerEntity({method.Reference("org.axonframework.eventsourcing.configuration.EventSourcedEntityModule")}.autodetected({new CommandJavaTypes(context.Request.Model.Application).Reference(source.JavaType, method)}.class, {name}Stream.class));");
            method.Line($"configuration.messaging(messaging -> messaging.registerCommandHandlingModule({method.Reference("org.axonframework.messaging.commandhandling.configuration.CommandHandlingModule")}.named({JavaCodeBuilder.StringLiteral(name)})")
                .Line($"    .commandHandlers().autodetectedCommandHandlingComponent(components -> handler({(usesClock ? "clock" : string.Empty)})).build()));")
                .Line($"fixture = {method.Reference("org.axonframework.test.fixture.AxonTestFixture")}.with(configuration);").Line("return fixture;");
        });
    }

    void FixtureClock(JavaCodeBuilder body)
    {
        if (usesClock) body.Line("var clock = java.time.Clock.fixed(java.time.OffsetDateTime.parse(instant).toInstant(), java.time.ZoneOffset.UTC);");
    }

    void Specification(JavaCodeBuilder body, SemanticSpecification specification, string testClass, int index)
    {
        var values = new CommandFixtureValues(context, command, specification, source);
        var input = values.Command();
        var given = specification.GivenEvents.Select(values.Event).ToArray();
        var expected = specification.ThenEvents.Select(values.Event).ToArray();
        var reason = Unsupported(specification, values);
        if (MissingValues(input, given, expected)) reason ??= "construct the complete typed command and event fixtures";
        var method = "spec_" + JavaNames.Member(specification.Name) + "_" + index;
        var code = reason is null ? null : AxonRefusalCodes.CommandSpecification;
        AxonSpecificationsEmitter.Register(context, specification.Id, testClass, method, code);
        body.Line($"@{body.Reference("org.junit.jupiter.api.Test")}").Line("@" + body.Reference("org.junit.jupiter.api.DisplayName") + "(" + JavaCodeBuilder.StringLiteral(specification.Id.ToString()) + ")");
        if (reason is not null)
        {
            context.Degradation.Unsupported(code!, specification.Id, "Axon cannot yet " + reason + ".", reason, specification.Id.ToString());
            body.Line("@" + body.Reference("org.junit.jupiter.api.Disabled") + "(" + JavaCodeBuilder.StringLiteral(code + ": " + reason) + ")").Block("void " + method + "()", test =>
                test.UnsupportedIntent(reason, specification.Id.ToString(), code!).Line("throw new " + test.Reference("java.lang.UnsupportedOperationException") + "(" + JavaCodeBuilder.StringLiteral(code + ": " + reason) + ");"));
            return;
        }
        body.Block("void " + method + "()", test =>
        {
            CommandFixtureSecurity.CallerReferences(test, specification.GivenCaller);
            var activity = given.Length == 0 ? ".noPriorActivity()" : $".events({string.Join(", ", given)})";
            test.Line($"var scenario = fixture({JavaCodeBuilder.StringLiteral(specification.GivenClock ?? "1970-01-01T00:00:00Z")}).given(){activity};")
                .Line($"var result = scenario.when().command({input}).then();");
            new CommandFixtureAssertions(context, command, specification).EmitReferences(test, [.. expected.Select(value => value!)]);
        });
    }

    bool HasUnsupportedCaptures(SemanticSpecification specification) =>
        !specification.GivenCaptures.IsEmpty || specification.GivenGenerated.Any(value => value.Command != command.Id);

    bool MissingStreamIdentity(SemanticSpecification specification, CommandFixtureValues values)
    {
        if (source?.Input is not { } input) return false;
        if (source.Generated) return false;
        return (values.Input(input) ?? specification.When?.EventSource?.Value) is null or SemanticNullValue;
    }

    string? Unsupported(SemanticSpecification specification, CommandFixtureValues values)
    {
        if (context.Degradation.UnsupportedConstructs.Any(diagnostic => diagnostic.Artifact == command.Id)) return "verify a command whose implementation or guarantee is degraded";
        if (!specification.GivenEvents.IsEmpty && Emitters.Constraints.ConstraintCoverage.All(context).Any(item => command.Produces.Any(produced => Emitters.Constraints.ConstraintCoverage.Applies(item.Constraint, produced.EventContract))))
            return "seed append-time uniqueness from historical fixtures";
        if (HasReadModelFixtures(specification))
            return "verify command execution together with persisted read-model fixtures";
        if (HasUnsupportedCaptures(specification)) return "verify capture or invoked-command generation fixtures";
        if (MissingStreamIdentity(specification, values))
            return "validate an absent stream identity before Axon resolves the entity";
        return AssertionRequirement(specification);
    }

    string? AssertionRequirement(SemanticSpecification specification)
    {
        if (specification.ThenErrors.Any(error => error.Code is not null)) return "verify stable validation error codes";
        if (!new CommandFixtureAssertions(context, command, specification).CanRenderResponse()) return "verify the complete modeled command response";
        if (specification.GivenEvents.Concat(specification.ThenEvents).Any(occurrence => occurrence.Route is not null || occurrence.Unrouted)) return "verify explicit event substream routes";
        return null;
    }
}
