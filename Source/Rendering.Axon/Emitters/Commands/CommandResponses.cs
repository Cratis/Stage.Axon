// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands;

internal sealed class CommandResponses(SemanticCommand command, CommandJavaTypes types)
{
    internal string ReturnType => command.Response switch
    {
        SemanticScalarCommandResponse scalar => types.Type(scalar.Type),
        SemanticRecordCommandResponse => JavaNames.Type(command.Name) + "Response",
        _ => "void"
    };

    internal string? Value => command.Response switch
    {
        null => string.Empty,
        SemanticScalarCommandResponse scalar => Property(scalar.Source, scalar.Type),
        SemanticRecordCommandResponse record => Record(record),
        _ => null
    };

    internal void Emit(AxonEmissionContext context, string package)
    {
        if (command.Response is not SemanticRecordCommandResponse record) return;
        var fields = record.Fields.Select(field => $"@com.fasterxml.jackson.annotation.JsonProperty({JavaCodeBuilder.StringLiteral(field.Name)}) {types.Type(field.Type)} {JavaNames.Member(field.Name)}");
        context.AddJava(package, ReturnType, new JavaCodeBuilder(package).Line($"public record {ReturnType}({string.Join(", ", fields)}) {{}}").Build(), sources: [command.Id]);
    }

    string? Property(SemanticId id, SemanticTypeReference type) => new CommandExpressions(command, types).Render(SemanticExpression.Property(SemanticExpressionRootKind.Command, id), type);

    string? Record(SemanticRecordCommandResponse record)
    {
        var values = record.Fields.Select(field => Property(field.Source, field.Type)).ToArray();
        return values.Any(value => value is null) ? null : $"new {ReturnType}({string.Join(", ", values)})";
    }
}
