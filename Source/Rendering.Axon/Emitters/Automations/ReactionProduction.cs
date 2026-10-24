// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Events;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Automations;

internal sealed record ReactionProduction(string Name, ReactionProducedEvent Event, (string ComponentName, string JavaType, string TagKey) Primary)
{
    internal string Invocation => (Event.Condition is null ? string.Empty : $"if ({Event.Condition}) ") + $"commands.sendAndWait(new {Name}({Event.Source}, {Event.Expression}));";

    internal void Emit(AxonEmissionContext context, string package, SemanticId reaction)
    {
        var command = new JavaCodeBuilder(package)
            .Line($"public record {Name}(@org.axonframework.modelling.annotation.TargetEntityId {Primary.JavaType} eventSourceId, {Event.Type} event) {{}}")
            .Build();
        context.AddJava(package, Name, command, sources: [reaction]);
        var handler = new JavaCodeBuilder(package)
            .Line("@org.springframework.stereotype.Component")
            .Block($"public class {Name}Handler", body => body.Line("@org.axonframework.messaging.commandhandling.annotation.CommandHandler")
                .Block($"public void handle({Name} command, org.axonframework.messaging.eventhandling.gateway.EventAppender appender, @org.axonframework.modelling.annotation.InjectEntity(idProperty = \"eventSourceId\") org.axonframework.modelling.repository.ManagedEntity<{Primary.JavaType}, {Name}Stream> stream)", method => method
                    .Line($"if (!java.util.Objects.equals(command.eventSourceId(), command.event().{Primary.ComponentName}())) throw new IllegalArgumentException(\"Event source does not match append boundary\");")
                    .Line("appender.append(command.event());")))
            .Build();
        context.AddJava(package, Name + "Handler", handler, sources: [reaction]);
        EmitStream(context, package, reaction);
    }

    void EmitStream(AxonEmissionContext context, string package, SemanticId reaction)
    {
        var contracts = ScopeSelection.Locate(context.Request.Model.Application).SelectMany(slice => slice.Slice.Events).DistinctBy(contract => contract.Id);
        var stream = new JavaCodeBuilder(package)
            .Line($"@org.axonframework.eventsourcing.annotation.EventSourcedEntity(tagKey = {JavaCodeBuilder.StringLiteral(Primary.TagKey)})")
            .Line($"@org.axonframework.extension.spring.stereotype.EventSourced(idType = {Primary.JavaType}.class, tagKey = {JavaCodeBuilder.StringLiteral(Primary.TagKey)})")
            .Block($"public class {Name}Stream", body =>
            {
                body.Line("@org.axonframework.eventsourcing.annotation.reflection.EntityCreator").Line($"public {Name}Stream() {{}}");
                var index = 0;
                foreach (var contract in contracts)
                    body.Line("@org.axonframework.eventsourcing.annotation.EventSourcingHandler").Line($"public void on{index++}({AxonEventsEmitter.TypeFor(context, contract.Id)} event) {{}}");
            }).Build();
        context.AddJava(package, Name + "Stream", stream, sources: [reaction]);
    }
}
