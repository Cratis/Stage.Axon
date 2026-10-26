// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Events;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Automations;

internal sealed record ReactionProduction(string Name, ReactionProducedEvent Event, (string ComponentName, string JavaType, string TagKey) Primary, string TagReference)
{
    internal string Invocation => (Event.Condition is null ? string.Empty : $"if ({Event.Condition}) ") + $"commands.sendAndWait(new {Name}({Event.Source}, {Event.Expression}));";

    internal void Emit(AxonEmissionContext context, string package, SemanticId reaction)
    {
        var command = new JavaCodeBuilder(package).Declare(Name);
        ReactionJavaReferences.Claim(command, [Primary.JavaType, Event.Type]);
        command.Line($"public record {Name}(@{command.Reference("org.axonframework.modelling.annotation.TargetEntityId")} {ReactionJavaReferences.Expression(command, Primary.JavaType)} eventSourceId, {ReactionJavaReferences.Expression(command, Event.Type)} event) {{}}");
        context.AddJava(package, Name, command.Build(), sources: [reaction]);
        var handler = new JavaCodeBuilder(package).Declare(Name).Declare(Name + "Handler").Declare(Name + "Stream");
        ReactionJavaReferences.Claim(handler, [Primary.JavaType]);
        var uniqueValues = handler.Reference(context.BasePackage + ".runtime.UniqueValueStore");
        var inMemoryUniqueValues = handler.Reference(context.BasePackage + ".runtime.InMemoryUniqueValueStore");
        var constraints = handler.Reference(context.BasePackage + ".infrastructure.ModeledConstraints");
        handler.Line("@" + handler.Reference("org.springframework.stereotype.Component"))
            .Block($"public class {Name}Handler", body => body
                .Line($"private final {uniqueValues} uniqueValues;")
                .Line($"public {Name}Handler() {{ uniqueValues = new {inMemoryUniqueValues}(); }}")
                .Line("@" + body.Reference("org.springframework.beans.factory.annotation.Autowired"))
                .Line($"public {Name}Handler({uniqueValues} uniqueValues) {{ this.uniqueValues = uniqueValues; }}")
                .Line("@" + body.Reference("org.axonframework.messaging.commandhandling.annotation.CommandHandler"))
                .Block($"public void handle({Name} command, {body.Reference("org.axonframework.messaging.eventhandling.gateway.EventAppender")} appender, {body.Reference("org.axonframework.messaging.core.unitofwork.ProcessingContext")} processingContext, @{body.Reference("org.axonframework.modelling.annotation.InjectEntity")}(idProperty = \"eventSourceId\") {body.Reference("org.axonframework.modelling.repository.ManagedEntity")}<{ReactionJavaReferences.Expression(body, Primary.JavaType)}, {Name}Stream> stream)", method => method
                    .Line($"if (!{method.Reference("java.util.Objects")}.equals(command.eventSourceId(), command.event().{Primary.ComponentName}())) throw new IllegalArgumentException(\"Event source does not match append boundary\");")
                    .Line($"{constraints}.apply(uniqueValues, command.event(), processingContext);")
                    .Line("appender.append(command.event());")));
        context.AddJava(package, Name + "Handler", handler.Build(), sources: [reaction]);
        EmitStream(context, package, reaction);
    }

    void EmitStream(AxonEmissionContext context, string package, SemanticId reaction)
    {
        var contracts = ScopeSelection.Locate(context.Request.Model.Application).SelectMany(slice => slice.Slice.Events).DistinctBy(contract => contract.Id).ToArray();
        var stream = new JavaCodeBuilder(package).Declare(Name + "Stream");
        ReactionJavaReferences.Claim(stream, contracts.Select(contract => AxonEventsEmitter.TypeFor(context, contract.Id)).Concat([Primary.JavaType, TagReference]));
        var tag = ReactionJavaReferences.Expression(stream, TagReference);
        stream.Line($"@{stream.Reference("org.axonframework.eventsourcing.annotation.EventSourcedEntity")}(tagKey = {tag})")
            .Line($"@{stream.Reference("org.axonframework.extension.spring.stereotype.EventSourced")}(idType = {ReactionJavaReferences.Expression(stream, Primary.JavaType)}.class, tagKey = {tag})")
            .Block($"public class {Name}Stream", body =>
            {
                body.Line("@" + body.Reference("org.axonframework.eventsourcing.annotation.reflection.EntityCreator")).Line($"public {Name}Stream() {{}}");
                var index = 0;
                foreach (var contract in contracts)
                    body.Line("@" + body.Reference("org.axonframework.eventsourcing.annotation.EventSourcingHandler")).Line($"public void on{index++}({ReactionJavaReferences.Expression(body, AxonEventsEmitter.TypeFor(context, contract.Id))} event) {{}}");
            });
        context.AddJava(package, Name + "Stream", stream.Build(), sources: [reaction]);
    }
}
