// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands;

internal sealed class CommandContractsEmitter
{
    public void Emit(AxonEmissionContext context)
    {
        var types = new CommandJavaTypes(context.Request.Model.Application);
        foreach (var located in ScopeSelection.Select(context.Request))
        {
            var package = Package(types, located);
            foreach (var command in located.Slice.Commands)
            {
                var name = JavaNames.Type(command.Name);
                context.Artifacts.Add(PlannedArtifact.CreateText(Path(package, name), Record(command, package, types), [command.Id, .. command.Properties.Select(property => property.Id)]));
                context.Artifacts.Add(PlannedArtifact.CreateText(Path(package, name + "Rules"), new CommandRulesEmitter(context, command, types).Render(package), [command.Id]));
            }
        }
    }

    internal static string Package(CommandJavaTypes types, LocatedSemanticSlice located) =>
        string.Join('.', new[] { types.BasePackage, JavaNames.PackageSegment(located.Module.Name) }.Concat(located.Features.Select(feature => JavaNames.PackageSegment(feature.Name))).Append(JavaNames.PackageSegment(located.Slice.Name)));

    internal static string Path(string package, string name) => $"src/main/java/{package.Replace('.', '/')}/{name}.java";

    static string Record(SemanticCommand command, string package, CommandJavaTypes types)
    {
        var components = command.Properties.Select(property =>
            $"{(property.Type.IsOptional || property.IsGenerated ? string.Empty : "@jakarta.validation.constraints.NotNull ")}@jakarta.validation.Valid {types.Type(property.Type)} {JavaNames.Member(property.Name)}");
        return new JavaCodeBuilder(package).Line($"public record {JavaNames.Type(command.Name)}({string.Join(", ", components)}) {{ }}").Build();
    }
}
