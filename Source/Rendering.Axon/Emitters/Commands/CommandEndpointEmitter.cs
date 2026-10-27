// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Security;
using Cratis.Stage.Rendering.Axon.Emitters.Specifications;
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
        var code = new JavaCodeBuilder(package).Declare(name).Declare(name + "Rules").Declare(name + "Endpoint");
        var returnType = ReturnType(command, new(context.Request.Model.Application), code);
        var gateway = code.Reference("org.axonframework.messaging.commandhandling.gateway.CommandGateway");
        code.Line($"@{code.Reference("org.springframework.web.bind.annotation.RestController")}").Block($"public class {name}Endpoint", body =>
        {
            body.Line($"private final {gateway} commandGateway;").Line($"private final {name}Rules rules;")
                .Block($"public {name}Endpoint({gateway} commandGateway, {name}Rules rules)", constructor => constructor.Line("this.commandGateway = commandGateway;").Line("this.rules = rules;"))
                .Line("@" + body.Reference("org.springframework.web.bind.annotation.PostMapping") + "(" + JavaCodeBuilder.StringLiteral(Route(located, command)) + ")");
            var expression = AxonHttpTestsEndpoints.Expression(Authorize(context, command, body));
            AxonHttpTestsEndpoints.Register(context, new("command", "POST", Route(located, command), null, expression, [], null));
            body.Block($"public {body.Reference("java.util.concurrent.CompletableFuture")}<{returnType}> execute(@{body.Reference("org.springframework.web.bind.annotation.RequestBody")} {name} request)", method =>
                method.Line("var command = " + (command.Properties.Any(property => property.IsGenerated) || source?.Allocated == true ? "request.forRequest();" : "request;"))
                    .Line("rules.validateInput(command);").Line($"return commandGateway.send(command, {returnType}.class);"));
        });
        context.AddJava(package, name + "Endpoint", code.Build(), sources: [command.Id]);
    }

    internal static string Kebab(string name) => Separators.Replace(WordBoundary.Replace(name, "${lower}-${upper}"), "-").Trim('-').ToLowerInvariant();

    static string ReturnType(SemanticCommand command, CommandJavaTypes types, JavaCodeBuilder code)
    {
        if (command.Response is SemanticScalarCommandResponse scalar)
        {
            types.Claim(scalar.Type, code);
            return types.Type(scalar.Type, code);
        }
        if (command.Response is not SemanticRecordCommandResponse) return code.Reference("java.lang.Void");
        var name = JavaNames.Type(command.Name) + "Response";
        code.Declare(name);
        return name;
    }

    static string Authorize(AxonEmissionContext context, SemanticCommand command, JavaCodeBuilder code)
    {
        var authorization = SecurityAnnotations.For(context, command.Authorization, command.Id);
        if (authorization.Length == 0) return authorization;
        foreach (var line in authorization.Split('\n'))
        {
            code.Line(line.StartsWith("@PreAuthorize(", StringComparison.Ordinal)
                ? "@" + code.Reference("org.springframework.security.access.prepost.PreAuthorize") + line["@PreAuthorize".Length..] : line);
        }
        return authorization;
    }
}
