// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters.Security;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands;

internal sealed class CommandHandlerEmitter(AxonEmissionContext context, LocatedSemanticSlice located, SemanticCommand command, CommandSource? source)
{
    readonly CommandJavaTypes _types = new(context.Request.Model.Application);

    internal void Emit()
    {
        var package = CommandContractsEmitter.Package(_types, located);
        var name = JavaNames.Type(command.Name);
        var response = new CommandResponses(command, _types);
        response.Emit(context, package);
        var code = new JavaCodeBuilder(package).Import("org.springframework.stereotype.Component")
            .Import("org.springframework.security.access.prepost.PreAuthorize")
            .Import("org.axonframework.messaging.commandhandling.annotation.CommandHandler")
            .Import("org.axonframework.messaging.eventhandling.gateway.EventAppender");
        code.Line("@Component").Block($"public class {name}Handler", body =>
        {
            body.Line($"private final {name}Rules rules;").Line("private final java.time.Clock clock;")
                .Block($"public {name}Handler({name}Rules rules, java.time.Clock clock)", constructor => constructor.Line("this.rules = rules;").Line("this.clock = clock;"))
                .Line("@CommandHandler");
            var authorization = SecurityAnnotations.For(context, command.Authorization, command.Id);
            if (authorization.Length > 0) body.Line(authorization);
            body.Block($"public {response.ReturnType} handle({Parameters(name)})", method => Handle(method, response));
        });
        context.AddJava(package, name + "Handler", code.Build(), sources: [command.Id]);
    }

    string Parameters(string name)
    {
        var parameters = name + " command, EventAppender appender";
        if (source is null) return parameters;
        if (source.Generated) return parameters + ", org.axonframework.messaging.core.unitofwork.ProcessingContext context";
        return parameters + $", @org.axonframework.modelling.annotation.InjectEntity(idProperty = {JavaCodeBuilder.StringLiteral(source.Member)}) org.axonframework.modelling.repository.ManagedEntity<{source.JavaType}, {name}Stream> stream";
    }

    void Handle(JavaCodeBuilder body, CommandResponses response)
    {
        if (!Supported(body, response)) return;
        var appends = new List<(string Event, string? Condition)>();
        foreach (var produced in command.Produces)
        {
            var append = new CommandAppends(context, command, source!).Build(produced);
            if (append is null)
            {
                Unsupported(body, AxonRefusalCodes.CommandMapping, "append every modeled event with its identity and complete property mappings");
                return;
            }
            appends.Add(append.Value);
        }
        Prepare(body);
        body.Line("rules.validate(command);");
        if (appends.Count > 0) Append(body, appends);
        if (response.ReturnType != "void") body.Line("return " + response.Value + ";");
    }

    bool Supported(JavaCodeBuilder body, CommandResponses response)
    {
        if (command.Handler is not null) return Unsupported(body, AxonRefusalCodes.Implementation, "execute the attached command implementation");
        if (!command.Reads.IsEmpty) return Unsupported(body, AxonRefusalCodes.ProtectedReads, "check modeled decision reads before changing state");
        if (source is null && !command.Produces.IsEmpty) return Unsupported(body, AxonRefusalCodes.CommandMapping, "resolve the command's primary event-source identity");
        return response.Value is not null || Unsupported(body, AxonRefusalCodes.CommandMapping, "compute the complete modeled command response");
    }

    void Prepare(JavaCodeBuilder body)
    {
        if (command.Properties.Any(property => property.IsGenerated) || source?.Allocated == true) body.Line("command = command.withGenerated(clock);");
        if (source?.Generated != true) return;
        var name = JavaNames.Type(command.Name);
        body.Line($"context.component(org.axonframework.modelling.StateManager.class).loadManagedEntity({name}Stream.class, command.{source.Member}(), context).join();");
    }

    void Append(JavaCodeBuilder body, IReadOnlyList<(string Event, string? Condition)> appends)
    {
        MetadataDifferences(body);
        body.Line("var occurred = java.time.OffsetDateTime.ofInstant(clock.instant(), java.time.ZoneOffset.UTC);")
            .Line("var events = new java.util.ArrayList<Object>();");
        foreach (var append in appends)
        {
            if (append.Condition is null) body.Line($"events.add({append.Event});");
            else body.Block($"if ({append.Condition})", conditional => conditional.Line($"events.add({append.Event});"));
        }
        body.Line("if (!events.isEmpty()) appender.append(events);");
    }

    void MetadataDifferences(JavaCodeBuilder body)
    {
        if (command.Route is not null || command.Produces.Any(produced => produced.Route is not null))
            Difference(body, AxonRefusalCodes.Routing, "route events through modeled source and substream partitions", "Only the primary event-source tag is used; source/substream routing metadata is not represented.");
        if (command.Produces.Any(produced => !produced.Tags.IsEmpty))
            Difference(body, AxonRefusalCodes.Append, "append the additional modeled event tags", "Only the shared primary event-source component is tagged.");
    }

    void Difference(JavaCodeBuilder body, string code, string intent, string difference)
    {
        var path = command.Id + "/produces";
        context.Degradation.Unsupported(code, command.Id, difference, intent, path);
        body.DiffersFromScreenplay(difference).UnsupportedIntent(intent, path, code);
    }

    bool Unsupported(JavaCodeBuilder body, string code, string intent)
    {
        var path = command.Id + "/handler";
        context.Degradation.Unsupported(code, command.Id, "Axon cannot yet " + intent + ".", intent, path);
        body.UnsupportedIntent(intent, path, code).Line("throw new UnsupportedOperationException(" + JavaCodeBuilder.StringLiteral(code + ": " + intent) + ");");
        return false;
    }
}
