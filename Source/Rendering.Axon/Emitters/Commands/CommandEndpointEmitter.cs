// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Security;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands;

internal static partial class CommandEndpointEmitter
{
    [GeneratedRegex("(?<lower>[a-z0-9])(?<upper>[A-Z])", RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex WordBoundary { get; }

    [GeneratedRegex("[^A-Za-z0-9]+", RegexOptions.None, 1000)]
    private static partial Regex Separators { get; }

    internal static string Route(LocatedSemanticSlice located, SemanticCommand command) => "/api/" + string.Join(
        '/',
        new[] { JavaNames.PackageSegment(located.Module.Name) }.Concat(located.Features.Select(feature => JavaNames.PackageSegment(feature.Name))).Append(Kebab(command.Name)));

    internal static void Emit(AxonEmissionContext context, LocatedSemanticSlice located, SemanticCommand command, CommandSource? source)
    {
        var package = CommandContractsEmitter.Package(new(context.Request.Model.Application), located);
        var name = JavaNames.Type(command.Name);
        var code = new JavaCodeBuilder(package).Import("org.springframework.web.bind.annotation.RestController")
            .Import("org.springframework.web.bind.annotation.PostMapping").Import("org.springframework.web.bind.annotation.RequestBody")
            .Import("org.springframework.security.access.prepost.PreAuthorize").Import("org.axonframework.messaging.commandhandling.gateway.CommandGateway");
        code.Line("@RestController").Block($"public class {name}Endpoint", body =>
        {
            body.Line("private final CommandGateway commandGateway;").Line($"private final {name}Rules rules;")
                .Block($"public {name}Endpoint(CommandGateway commandGateway, {name}Rules rules)", constructor => constructor.Line("this.commandGateway = commandGateway;").Line("this.rules = rules;"))
                .Line("@PostMapping(" + JavaCodeBuilder.StringLiteral(Route(located, command)) + ")");
            var authorization = SecurityAnnotations.For(context, command.Authorization, command.Id);
            if (authorization.Length > 0) body.Line(authorization);
            body.Block($"public java.util.concurrent.CompletableFuture<Object> execute(@RequestBody {name} request)", method =>
                method.Line("var command = " + (command.Properties.Any(property => property.IsGenerated) || source?.Allocated == true ? "request.forRequest();" : "request;"))
                    .Line("rules.validateInput(command);").Line("return commandGateway.send(command).resultAs(Object.class);"));
        });
        context.AddJava(package, name + "Endpoint", code.Build(), sources: [command.Id]);
    }

    static string Kebab(string name) => Separators.Replace(WordBoundary.Replace(name, "${lower}-${upper}"), "-").Trim('-').ToLowerInvariant();
}
