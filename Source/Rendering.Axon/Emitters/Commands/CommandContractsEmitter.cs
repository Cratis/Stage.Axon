// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands;

internal static class CommandContractsEmitter
{
    internal static void EmitCommand(AxonEmissionContext context, LocatedSemanticSlice located, SemanticCommand command, CommandSource? source)
    {
        var types = new CommandJavaTypes(context.Request.Model.Application);
        var package = Package(types, located);
        var name = JavaNames.Type(command.Name);
        context.AddJava(package, name, Record(context, command, package, source), sources: [command.Id, .. command.Properties.Select(property => property.Id)]);
        context.AddJava(package, name + "Rules", new CommandRulesEmitter(context, command, types).Render(package), sources: [command.Id]);
    }

    internal static string Package(CommandJavaTypes types, LocatedSemanticSlice located) =>
        string.Join('.', new[] { types.BasePackage, JavaNames.PackageSegment(located.Module.Name) }.Concat(located.Features.Select(feature => JavaNames.PackageSegment(feature.Name))).Append(JavaNames.PackageSegment(located.Slice.Name)));

    internal static string JsonProperty(string name, JavaCodeBuilder code) => JavaNames.Member(name) == name
        ? string.Empty : $"@{code.Reference("com.fasterxml.jackson.annotation.JsonProperty")}({JavaCodeBuilder.StringLiteral(name)}) ";

    internal static string Code(AxonEmissionContext context, SemanticCommand command, string package, CommandSource? source)
    {
        var types = new CommandJavaTypes(context.Request.Model.Application);
        var code = new JavaCodeBuilder(package).Declare(JavaNames.Type(command.Name));
        foreach (var property in command.Properties) types.Claim(property.Type, code);
        if (source?.JavaType.StartsWith(types.BasePackage + ".", StringComparison.Ordinal) == true) code.Import(source.JavaType);
        var components = command.Properties.Select(property => Component(property, types, source, code)).ToList();
        if (source?.Allocated == true) components.Insert(0, $"@{code.Reference("com.fasterxml.jackson.annotation.JsonIgnore")} @{code.Reference("org.axonframework.modelling.annotation.TargetEntityId")} {types.Reference(source.JavaType, code)} {source.Member}");
        return code.Line($"@{code.Reference("org.axonframework.messaging.commandhandling.annotation.Command")}(name = {JavaCodeBuilder.StringLiteral(command.Name)})")
            .Block($"public record {JavaNames.Type(command.Name)}({string.Join(", ", components)})", body =>
        {
            if (source?.NeedsAccessor(command) == true) body.Line($"@{body.Reference("org.axonframework.modelling.annotation.TargetEntityId")}").Line($"public {types.Reference(source.JavaType, body)} {source.Member}() {{ return {source.Value(command, types, "this")}; }}");
            new CommandPreparation(context, command, source).Emit(body);
        }).Build();
    }

    static string Record(AxonEmissionContext context, SemanticCommand command, string package, CommandSource? source)
    {
        var types = new CommandJavaTypes(context.Request.Model.Application);
        var components = command.Properties.Select(property => Component(property, types, source)).ToList();
        if (source?.Allocated == true) components.Insert(0, $"@com.fasterxml.jackson.annotation.JsonIgnore @org.axonframework.modelling.annotation.TargetEntityId {source.JavaType} {source.Member}");
        return new JavaCodeBuilder(package)
            .Import("org.axonframework.messaging.commandhandling.annotation.Command")
            .Line($"@Command(name = {JavaCodeBuilder.StringLiteral(command.Name)})")
            .Block($"public record {JavaNames.Type(command.Name)}({string.Join(", ", components)})", body =>
        {
            if (source?.NeedsAccessor(command) == true) body.Line("@org.axonframework.modelling.annotation.TargetEntityId").Line($"public {source.JavaType} {source.Member}() {{ return {source.Value(command, types, "this")}; }}");
            new CommandPreparation(context, command, source).Emit(body);
        }).Build();
    }

    static string Component(SemanticProperty property, CommandJavaTypes types, CommandSource? source)
    {
        var required = property.Type.IsOptional || property.IsGenerated ? string.Empty : "@jakarta.validation.constraints.NotNull ";
        var generated = property.IsGenerated ? "@com.fasterxml.jackson.annotation.JsonIgnore " : string.Empty;
        var identifier = source?.Member == JavaNames.Member(property.Name) ? "@org.axonframework.modelling.annotation.TargetEntityId " : string.Empty;
        var cascaded = types.Cascades(property.Type) ? "@jakarta.validation.Valid " : string.Empty;
        return $"{identifier}{required}{cascaded}{generated}{types.Type(property.Type)} {JavaNames.Member(property.Name)}";
    }

    static string Component(SemanticProperty property, CommandJavaTypes types, CommandSource? source, JavaCodeBuilder code)
    {
        var required = property.Type.IsOptional || property.IsGenerated ? string.Empty : $"@{code.Reference("jakarta.validation.constraints.NotNull")} ";
        var generated = property.IsGenerated ? $"@{code.Reference("com.fasterxml.jackson.annotation.JsonIgnore")} " : string.Empty;
        var identifier = source?.Member == JavaNames.Member(property.Name) ? $"@{code.Reference("org.axonframework.modelling.annotation.TargetEntityId")} " : string.Empty;
        var cascaded = types.Cascades(property.Type) ? $"@{code.Reference("jakarta.validation.Valid")} " : string.Empty;
        return $"{identifier}{required}{cascaded}{generated}{types.Type(property.Type, code)} {JavaNames.Member(property.Name)}";
    }
}
