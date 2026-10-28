// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters.Constraints;
using Cratis.Stage.Rendering.Axon.Emitters.Decisions;
using Cratis.Stage.Rendering.Axon.Emitters.Events;
using Cratis.Stage.Rendering.Axon.Emitters.Implementations;
using Cratis.Stage.Rendering.Axon.Emitters.Runtime;
using Cratis.Stage.Rendering.Axon.Emitters.Security;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands;

internal sealed class CommandHandlerEmitter(AxonEmissionContext context, LocatedSemanticSlice located, SemanticCommand command, CommandSource? source)
{
    static readonly string[] _forbiddenAppenderTokens = ["EventAppender", "EventSink", "EventGateway", "EventStore", "forContext", "\\u"];
    readonly CommandJavaTypes _types = new(context.Request.Model.Application, context.BasePackage);

    bool _usesClock;
    string? _nativeBody;

    bool Constrained => ConstraintCoverage.All(context).Any(item => command.Produces.Any(produced => ConstraintCoverage.Applies(item.Constraint, produced.EventContract)));

    bool MissingSource => command.Handler is null && source is null && !command.Produces.IsEmpty;

    internal bool Emit()
    {
        var package = CommandContractsEmitter.Package(_types, located);
        var name = JavaNames.Type(command.Name);
        var response = new CommandResponses(command, _types);
        response.Emit(context, package);
        var code = new JavaCodeBuilder(package).Declare(name).Declare(name + "Handler").Declare(name + "Stream");
        ClaimTypes(code, response);
        code.Line($"@{code.Reference("org.springframework.stereotype.Component")}").Block($"public class {name}Handler", body =>
        {
            body.Line($"@{body.Reference("org.axonframework.messaging.commandhandling.annotation.CommandHandler")}");
            var authorization = SecurityAnnotations.For(context, command.Authorization, command.Id);
            if (authorization.Length > 0) body.Line(authorization.Replace("@PreAuthorize", "@" + body.Reference("org.springframework.security.access.prepost.PreAuthorize"), StringComparison.Ordinal));
            body.Block($"public {response.ReturnTypeFor(body)} handle({Parameters(name, body, package)})", method => Handle(method, response, package));
            if (_nativeBody is not null) NativeBody(body, response, name);
            Dependencies(body, name);
        });
        context.AddJava(package, name + "Handler", code.Build(), sources: [command.Id]);
        return _usesClock;
    }

    void ClaimTypes(JavaCodeBuilder code, CommandResponses response)
    {
        code.ClaimExpressions([response.ReturnType, response.Value ?? string.Empty]);
        new CommandDomainChecks(context, command, _types).Claim(code);
        if (Constrained) code.ClaimExpressions([AxonRuntimePackage.Type(context.BasePackage, "UniqueValueStore"), AxonRuntimePackage.Type(context.BasePackage, "InMemoryUniqueValueStore"), context.BasePackage + ".infrastructure.ModeledConstraints"]);
        foreach (var produced in command.Produces)
        {
            var contract = CommandAppends.Contract(context, produced.EventContract);
            code.ClaimExpressions([AxonEventsEmitter.TypeFor(context, contract.Id), .. contract.Properties.Select(property => _types.Type(property.Type))]);
        }
    }

    void Dependencies(JavaCodeBuilder body, string name)
    {
        if (_usesClock) body.Line($"private final {body.Reference("java.time.Clock")} clock;");
        if (Constrained) body.Line($"private final {body.Reference(AxonRuntimePackage.Type(context.BasePackage, "UniqueValueStore"))} uniqueValues;");
        var parameters = _usesClock ? $"{body.Reference("java.time.Clock")} clock" : string.Empty;
        body.Block($"public {name}Handler({parameters})", constructor =>
        {
            if (_usesClock) constructor.Line("this.clock = clock;");
            if (Constrained) constructor.Line($"this.uniqueValues = new {constructor.Reference(AxonRuntimePackage.Type(context.BasePackage, "InMemoryUniqueValueStore"))}();");
        });
        if (!Constrained) return;
        body.Line($"@{body.Reference("org.springframework.beans.factory.annotation.Autowired")}")
            .Block($"public {name}Handler({parameters}{(_usesClock ? ", " : string.Empty)}{body.Reference(AxonRuntimePackage.Type(context.BasePackage, "UniqueValueStore"))} uniqueValues)", constructor =>
            {
                if (_usesClock) constructor.Line("this.clock = clock;");
                constructor.Line("this.uniqueValues = uniqueValues;");
            });
    }

    string SubjectParameter(JavaCodeBuilder code) => !CommandAppends.UsesSubject(command) ? string.Empty : $", @{code.Reference("org.axonframework.messaging.core.annotation.MetadataValue")}(\"username\") {code.Reference("java.lang.String")} username";

    string Parameters(string name, JavaCodeBuilder code, string package)
    {
        var appender = command.Handler is not null && Constrained ? "unconstrainedAppender" : "appender";
        var parameters = name + $" command, {code.Reference("org.axonframework.messaging.eventhandling.gateway.EventAppender")} {appender}";
        if (Constrained && source?.Generated != true) parameters += $", {code.Reference("org.axonframework.messaging.core.unitofwork.ProcessingContext")} context";
        parameters += SubjectParameter(code);
        if (source is null) return parameters;
        if (source.Generated) return parameters + $", {code.Reference("org.axonframework.messaging.core.unitofwork.ProcessingContext")} context";
        var selector = source.TagKey == source.Member ? "(idProperty = " + source.ReferenceTag(code, package) + ")" : string.Empty;
        return parameters + $", @{code.Reference("org.axonframework.modelling.annotation.InjectEntity")}{selector} @{code.Reference("org.jspecify.annotations.Nullable")} {name}Stream state";
    }

    void Handle(JavaCodeBuilder body, CommandResponses response, string package)
    {
        if (!Supported(body, response)) return;
        if (command.Handler is { } handler)
        {
            HandleNative(body, handler, package, response);
            return;
        }
        var appends = new List<(string Event, string? Condition)>();
        var builder = new CommandAppends(context, command, source!);
        foreach (var produced in command.Produces)
        {
            var append = builder.Build(produced, body);
            if (append is null)
            {
                Unsupported(body, AxonRefusalCodes.CommandMapping, "append every modeled event with its identity and complete property mappings");
                return;
            }
            appends.Add(append.Value);
        }
        if (!Validate(body)) return;
        if (appends.Count > 0) Append(body, appends, builder.UsesOccurred);
        if (response.ReturnType != "void") body.Line("return " + body.ReferenceExpression(response.Value!) + ";");
    }

    bool ResolveNative(JavaCodeBuilder body, SemanticCommandHandler handler, out string content)
    {
        content = string.Empty;
        var slot = new InlineBodySlot(handler.RequirementId, SemanticImplementationRole.CommandHandler, command.Id);
        if (handler.ResultVersion != 1) return Unsupported(body, AxonRefusalCodes.Implementation, "execute the attached command implementation with an unsupported result revision");
        if (!InlineJavaBodies.TryResolve(context, slot, out var resolved, out var reason)) return Unsupported(body, AxonRefusalCodes.Implementation, "execute the attached command implementation: " + reason);
        content = resolved;
        if (!Constrained || !_forbiddenAppenderTokens.Any(token => resolved.Contains(token, StringComparison.Ordinal))) return true;
        Unsupported(body, AxonRefusalCodes.Constraint, "enforce modeled uniqueness for every event a native command body appends");
        return false;
    }

    void HandleNative(JavaCodeBuilder body, SemanticCommandHandler handler, string package, CommandResponses response)
    {
        if (!ResolveNative(body, handler, out var content) || !Validate(body)) return;
        foreach (var produced in command.Produces) AxonEventsEmitter.TypeFor(context, CommandAppends.Contract(context, produced.EventContract).Id, body, package);
        if (Constrained)
        {
            var arguments = "command, " + body.Reference(context.BasePackage + ".infrastructure.ModeledConstraints") + ".enforcing(uniqueValues, unconstrainedAppender, context)";
            if (source?.Generated == true) arguments += ", context";
            else if (source is not null) arguments += ", state";
            body.Line((response.ReturnType == "void" ? string.Empty : "return ") + "handleBody(" + arguments + ");");
            _nativeBody = content;
        }
        else
        {
            body.RawBody(content);
        }
        context.ConsumedImplementations.Add(handler.RequirementId);
    }

    void NativeBody(JavaCodeBuilder body, CommandResponses response, string name)
    {
        var parameters = name + $" command, {body.Reference("org.axonframework.messaging.eventhandling.gateway.EventAppender")} appender";
        if (source?.Generated == true) parameters += $", {body.Reference("org.axonframework.messaging.core.unitofwork.ProcessingContext")} context";
        else if (source is not null) parameters += $", @{body.Reference("org.jspecify.annotations.Nullable")} {name}Stream state";
        body.Block($"private {response.ReturnTypeFor(body)} handleBody({parameters})", method => method.RawBody(_nativeBody!));
    }

    bool Supported(JavaCodeBuilder body, CommandResponses response)
    {
        if (ConstraintCoverage.CommandRequiresUnsupportedConstraint(context, command)) return Unsupported(body, AxonRefusalCodes.Constraint, "enforce or release modeled uniqueness in the append unit of work");
        if (new CommandDecisionReads(context, command, source).Prepare() is null) return Unsupported(body, AxonRefusalCodes.ProtectedReads, "check modeled decision reads before changing state");
        if (MissingSource) return Unsupported(body, AxonRefusalCodes.CommandMapping, "resolve the command's primary event-source identity");
        return command.Handler is not null || response.Value is not null || Unsupported(body, AxonRefusalCodes.CommandMapping, "compute the complete modeled command response");
    }

    bool Validate(JavaCodeBuilder body)
    {
        new CommandDecisionReads(context, command, source).Check(body);
        Prepare(body);
        return new CommandDomainChecks(context, command, _types).Emit(body);
    }

    void Prepare(JavaCodeBuilder body)
    {
        if (command.Properties.Any(property => property.IsGenerated) || source?.Allocated == true)
        {
            _usesClock = true;
            body.Line("command = command.withGenerated(clock);");
        }
        if (source?.Generated != true) return;
        var name = JavaNames.Type(command.Name);
        body.Line($"context.component({body.Reference("org.axonframework.modelling.StateManager")}.class).loadManagedEntity({name}Stream.class, command.{source.Member}(), context).join();");
    }

    void Append(JavaCodeBuilder body, List<(string Event, string? Condition)> appends, bool usesOccurred)
    {
        MetadataDifferences(body);
        if (usesOccurred)
        {
            _usesClock = true;
            body.Line($"var occurred = {body.Reference("java.time.OffsetDateTime")}.ofInstant(clock.instant(), {body.Reference("java.time.ZoneOffset")}.UTC);");
        }
        if (!Constrained && appends is [{ Condition: null }])
        {
            body.Line($"appender.append({body.ReferenceExpression(appends[0].Event)});");
            return;
        }
        body.Line($"var events = new {body.Reference("java.util.ArrayList")}<{body.Reference("java.lang.Object")}>();");
        foreach (var append in appends)
        {
            if (append.Condition is null) body.Line($"events.add({body.ReferenceExpression(append.Event)});");
            else body.Block($"if ({append.Condition})", conditional => conditional.Line($"events.add({conditional.ReferenceExpression(append.Event)});"));
        }
        if (Constrained) body.Line($"for (var event : events) {body.Reference(context.BasePackage + ".infrastructure.ModeledConstraints")}.apply(uniqueValues, event, context);");
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
