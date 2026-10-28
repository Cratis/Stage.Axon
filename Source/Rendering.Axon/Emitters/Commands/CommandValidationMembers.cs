// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands;

internal sealed class CommandValidationMembers(SemanticApplication application, SemanticCommand command)
{
    readonly Member[] _members = Members(application, command);

    internal string PathExpression => Required ? "validationMember(violation.getPropertyPath().toString())" : "violation.getPropertyPath().toString()";

    bool Required => _members.Any(member => member.Renamed);

    internal void Emit(JavaCodeBuilder builder)
    {
        if (!Required) return;
        builder.Line().Block($"private static {builder.Reference("java.lang.String")} validationMember({builder.Reference("java.lang.String")} path)", method =>
        {
            method.Line($"var result = new {method.Reference("java.lang.StringBuilder")}();").Line("int type = 0;")
                .Block("for (var segment : path.split(\"\\\\.\", -1))", loop =>
                {
                    loop.Line("int bracket = segment.indexOf('[');")
                        .Line("var member = bracket < 0 ? segment : segment.substring(0, bracket);")
                        .Line("var suffix = bracket < 0 ? \"\" : segment.substring(bracket);")
                        .Line("var key = type + \":\" + member;");
                    Switch(loop, "type", _members.Where(member => member.Next is not null).Select(member => (member.Key, $"{member.Next}")), "-1");
                    Switch(loop, "member", _members.Where(member => member.Renamed).Select(member => (member.Key, JavaCodeBuilder.StringLiteral(member.Name))), "member");
                    loop.Line("if (!result.isEmpty()) result.append('.');").Line("result.append(member).append(suffix);");
                }).Line("return result.toString();");
        });
    }

    static void Switch(JavaCodeBuilder builder, string target, IEnumerable<(string Key, string Value)> entries, string fallback)
    {
        builder.Line($"{target} = switch (key) {{");
        foreach (var (key, value) in entries) builder.Line($"    case {JavaCodeBuilder.StringLiteral(key)} -> {value};");
        builder.Line($"    default -> {fallback};").Line("};");
    }

    static Member[] Members(SemanticApplication application, SemanticCommand command)
    {
        var shapes = new List<ImmutableArray<SemanticProperty>> { command.Properties };
        var types = new Dictionary<SemanticId, int>();
        var members = new List<Member>();
        for (var index = 0; index < shapes.Count; index++)
        {
            foreach (var property in shapes[index])
            {
                members.Add(new($"{index}:{JavaNames.Member(property.Name)}", property.Name, JavaNames.Member(property.Name) != property.Name, Next(property.Type)));
            }
        }
        return [.. members];

        int? Next(SemanticTypeReference type)
        {
            if (type.Kind != SemanticTypeReferenceKind.CompositeType) return null;
            if (types.TryGetValue(type.Target, out var target)) return target;
            target = shapes.Count;
            types.Add(type.Target, target);
            shapes.Add(application.Types.Single(composite => composite.Id == type.Target).Properties);
            return target;
        }
    }

    sealed record Member(string Key, string Name, bool Renamed, int? Next);
}
