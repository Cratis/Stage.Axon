// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Decisions;
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
        var decisions = new CommandDecisionReads(context, command, source).Prepare() ?? [];
        var required = decisions.SelectMany(read => read.Events.All).ToHashSet();
        var contracts = ScopeSelection.Locate(context.Request.Model.Application)
            .SelectMany(slice => slice.Slice.Events.Where(contract => slice.Module.Id == located.Module.Id || required.Contains(contract.Id)))
            .DistinctBy(contract => contract.Id).ToArray();
        var code = new JavaCodeBuilder(package).Declare(name);
        var tag = source.TagReference;
        var events = contracts.ToDictionary(contract => contract.Id, contract => AxonEventsEmitter.TypeFor(context, contract.Id, code, package));
        if (source.JavaType.StartsWith(context.BasePackage + ".", StringComparison.Ordinal)) code.Import(source.JavaType);
        var sourceType = new CommandJavaTypes(context.Request.Model.Application).Reference(source.JavaType, code);
        code.Line($"@{code.Reference("org.axonframework.eventsourcing.annotation.EventSourcedEntity")}(tagKey = {tag})")
            .Line($"@{code.Reference("org.axonframework.extension.spring.stereotype.EventSourced")}(idType = {sourceType}.class, tagKey = {tag})")
            .Block($"public class {name}", body =>
            {
                body.Line($"@{body.Reference("org.axonframework.eventsourcing.annotation.reflection.EntityCreator")}").Line($"public {name}() {{}}")
                    .Line("// Replay the module's event types so the append condition covers the entire primary stream position.");
                foreach (var read in decisions) body.Line($"private boolean {read.Member};").Line($"public boolean {read.Member}() {{ return {read.Member}; }}");
                foreach (var contract in contracts)
                {
                    var updates = string.Join(' ', decisions.Where(read => read.Events.All.Contains(contract.Id)).Select(read => read.Member + " = " + (read.Events.Removes.Contains(contract.Id) ? "false;" : "true;")));
                    body.Line($"@{body.Reference("org.axonframework.eventsourcing.annotation.EventSourcingHandler")}")
                        .Line($"public void on({events[contract.Id]} event) {{{updates}}}");
                }
            });
        context.AddJava(package, name, code.Build(), sources: [command.Id]);
    }
}
