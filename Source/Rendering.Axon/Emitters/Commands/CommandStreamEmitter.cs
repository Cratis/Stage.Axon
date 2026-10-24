// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Events;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands;

internal static class CommandStreamEmitter
{
    internal static void Emit(AxonEmissionContext context, LocatedSemanticSlice located, SemanticCommand command, CommandSource source)
    {
        var package = CommandContractsEmitter.Package(new(context.Request.Model.Application), located);
        var name = JavaNames.Type(command.Name) + "Stream";
        var contracts = ScopeSelection.Locate(context.Request.Model.Application).Where(slice => slice.Module.Id == located.Module.Id)
            .SelectMany(slice => slice.Slice.Events).DistinctBy(contract => contract.Id).ToArray();
        var code = new JavaCodeBuilder(package)
            .Import("org.axonframework.extension.spring.stereotype.EventSourced")
            .Import("org.axonframework.eventsourcing.annotation.reflection.EntityCreator")
            .Import("org.axonframework.eventsourcing.annotation.EventSourcingHandler")
            .Import("org.axonframework.eventsourcing.annotation.EventSourcedEntity")
            .Line($"@EventSourcedEntity(tagKey = {JavaCodeBuilder.StringLiteral(source.TagKey)})")
            .Line($"@EventSourced(idType = {source.JavaType}.class, tagKey = {JavaCodeBuilder.StringLiteral(source.TagKey)})")
            .Block($"public class {name}", body =>
            {
                body.Line("@EntityCreator").Line($"public {name}() {{}}")
                    .Line("// Replay the module's event types so the append condition covers the entire primary stream position.");
                for (var index = 0; index < contracts.Length; index++)
                {
                    body.Line("@EventSourcingHandler")
                        .Line($"public void on{index}({AxonEventsEmitter.TypeFor(context, contracts[index].Id)} event) {{}}");
                }
            });
        context.AddJava(package, name, code.Build(), sources: [command.Id]);
    }
}
