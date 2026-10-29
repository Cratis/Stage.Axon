// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Constraints;
using Cratis.Stage.Rendering.Axon.Emitters.Events;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Automations;

internal sealed record ReactionProduction(string Name, ReactionProducedEvent Event, (string ComponentName, string JavaType, string TagKey) Primary, IReadOnlyList<(SemanticConstraint Constraint, string? Value, string ValueType)>? Claims = null)
{
    internal string Invocation(bool withContext) => (Event.Condition is null ? string.Empty : $"if ({Event.Condition}) ") + $"commands.sendAndWait(new {Name}({Event.Source}, {Event.Expression}){(withContext ? ", context" : string.Empty)});";

    internal string InvocationWithMetadata(bool withContext) => (Event.Condition is null ? string.Empty : $"if ({Event.Condition}) ") + $"commands.sendAndWait(new {Name}({Event.Source}, {Event.Expression}), java.lang.Object.class, metadata, {(withContext ? "context" : "null")});";

    internal void Emit(AxonEmissionContext context, string package, SemanticId reaction)
    {
        var command = new JavaCodeBuilder(package).Declare(Name);
        var claims = Claims ?? [];
        ReactionJavaReferences.Claim(command, [Primary.JavaType, Event.Type, .. claims.Select(claim => claim.ValueType)]);
        var declaration = $"public record {Name}(@{command.Reference("org.axonframework.modelling.annotation.TargetEntityId")} {ReactionJavaReferences.Expression(command, Primary.JavaType)} eventSourceId, {ReactionJavaReferences.Expression(command, Event.Type)} event)";
        if (claims.Count == 0)
        {
            command.Line(declaration + " {}");
        }
        else
        {
            command.Block(declaration, record =>
            {
                foreach (var claim in claims.Where(claim => claim.Value is not null))
                    record.Line($"public {ReactionJavaReferences.Expression(record, claim.ValueType)} {Key(claim.Constraint)}() {{ return event.{claim.Value}(); }}");
            });
        }
        context.AddJava(package, Name, command.Build(), sources: [reaction]);
        var handler = new JavaCodeBuilder(package).Declare(Name).Declare(Name + "Handler");
        ReactionJavaReferences.Claim(handler, claims.Select(claim => ConstraintEntities.Qualified(context, claim.Constraint)));
        var entities = string.Concat(claims.Select(claim => $", @{handler.Reference("org.axonframework.modelling.annotation.InjectEntity")}(idProperty = {JavaCodeBuilder.StringLiteral(claim.Value is null ? "eventSourceId" : Key(claim.Constraint))}) {handler.Reference(ConstraintEntities.Qualified(context, claim.Constraint))} {JavaNames.Member(claim.Constraint.Name)}"));
        handler.Line("@" + handler.Reference("org.springframework.stereotype.Component"))
            .Block($"public class {Name}Handler", body => body
                .Line("@" + body.Reference("org.axonframework.messaging.commandhandling.annotation.CommandHandler"))
                .Block($"public void handle({Name} command, {body.Reference("org.axonframework.messaging.eventhandling.gateway.EventAppender")} appender{entities})", method =>
                    {
                        method.Line($"if (!{method.Reference("java.util.Objects")}.equals(command.eventSourceId(), command.event().{Primary.ComponentName}())) throw new IllegalArgumentException(\"Event source does not match append boundary\");");
                        foreach (var claim in claims) method.Line(JavaNames.Member(claim.Constraint.Name) + "." + ConstraintEntities.Assert(claim.Constraint, claim.Value ?? string.Empty) + (claim.Value is null ? "();" : "(command.eventSourceId());"));
                        method.Line("appender.append(command.event());");
                    }));
        context.AddJava(package, Name + "Handler", handler.Build(), sources: [reaction]);
    }

    static string Key(SemanticConstraint constraint) => JavaNames.Member(constraint.Name) + "Key";
}
