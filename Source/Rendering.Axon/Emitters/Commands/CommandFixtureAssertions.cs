// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Runtime;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands;

internal sealed class CommandFixtureAssertions(AxonEmissionContext context, SemanticCommand command, SemanticSpecification specification)
{
    readonly CommandJavaTypes _types = new(context.Request.Model.Application, context.BasePackage);
    JavaCodeBuilder? _code;

    internal bool CanRenderResponse() => specification.ThenReturns switch
    {
        null => true,
        SemanticScalarSpecificationResponse scalar when command.Response is SemanticScalarCommandResponse response => _types.Literal(scalar.Value, response.Type) is not null,
        SemanticRecordSpecificationResponse record when command.Response is SemanticRecordCommandResponse response => record.Fields.All(field =>
            response.Fields.FirstOrDefault(candidate => candidate.Name == field.Name) is { } target && _types.Literal(field.Value, target.Type) is not null),
        _ => false
    };

    internal void Claim(JavaCodeBuilder code)
    {
        if (specification.ThenDenied) return;
        if (!specification.ThenErrors.IsEmpty)
        {
            code.Reference(AxonRuntimePackage.Type(context.BasePackage, "CommandValidationException"));
            return;
        }
        ClaimResponse(code);
    }

    internal void EmitReferences(JavaCodeBuilder method, IReadOnlyList<string> expected) => new CommandFixtureAssertions(context, command, specification) { _code = method }.Emit(method, expected);

    internal void Emit(JavaCodeBuilder method, IReadOnlyList<string> expected)
    {
        if (specification.ThenDenied)
        {
            method.Line($"result.exception({Reference("org.springframework.security.access.AccessDeniedException")}.class).noEvents();");
            return;
        }
        if (!specification.ThenErrors.IsEmpty)
        {
            Errors(method);
            return;
        }
        method.Line("result.success();");
        if (specification.ThenNoEvents) method.Line("result.noEvents();");
        else if (expected.Count > 0) Events(method, expected);
        Response(method);
    }

    string Reference(string type) => _code?.Reference(type) ?? type;

    string? Literal(SemanticValue value, SemanticTypeReference type) => _code is null ? _types.Literal(value, type) : _types.Literal(value, type, _code);

    void ClaimResponse(JavaCodeBuilder code)
    {
        if (specification.ThenReturns is SemanticScalarSpecificationResponse scalar && command.Response is SemanticScalarCommandResponse response) _types.ClaimLiteral(scalar.Value, response.Type, code);
        if (specification.ThenReturns is not SemanticRecordSpecificationResponse record || command.Response is not SemanticRecordCommandResponse contract) return;
        code.Declare(JavaNames.Type(command.Name) + "Response");
        foreach (var field in record.Fields) _types.ClaimLiteral(field.Value, contract.Fields.Single(candidate => candidate.Name == field.Name).Type, code);
    }

    void Errors(JavaCodeBuilder method)
    {
        var exceptionType = Reference(AxonRuntimePackage.Type(context.BasePackage, "CommandValidationException"));
        method.Block("result.exceptionSatisfies(error ->", assertion =>
        {
            assertion.Line($"{Reference("org.assertj.core.api.Assertions")}.assertThat(error).isInstanceOf({exceptionType}.class);")
                .Line($"var failure = ({exceptionType}) error;");
            foreach (var error in specification.ThenErrors.Where(error => error.Message is not null))
                assertion.Line($"{Reference("org.assertj.core.api.Assertions")}.assertThat(failure.errors()).extracting(validationError -> validationError.message()).contains({JavaCodeBuilder.StringLiteral(error.Message!)});");
        }).Line(");").Line("result.noEvents();");
    }

    void Events(JavaCodeBuilder method, IReadOnlyList<string> expected)
    {
        if (!specification.ThenEventsInAnyOrder)
        {
            method.Line($"result.events({string.Join(", ", expected)});");
            return;
        }
        method.Line($"result.eventsSatisfy(events -> {Reference("org.assertj.core.api.Assertions")}.assertThat(events.stream().map(event -> event.payload()).toList())")
            .Line($"    .containsExactlyInAnyOrder({string.Join(", ", expected)}));");
    }

    void Response(JavaCodeBuilder method)
    {
        if (specification.ThenReturns is SemanticScalarSpecificationResponse scalar && command.Response is SemanticScalarCommandResponse response)
        {
            method.Line($"result.resultMessagePayload({Literal(scalar.Value, response.Type)});");
        }
        else if (specification.ThenReturns is SemanticRecordSpecificationResponse record && command.Response is SemanticRecordCommandResponse contract)
        {
            method.Block($"result.resultMessagePayloadSatisfies({JavaNames.Type(command.Name)}Response.class, response ->", assertion =>
            {
                foreach (var field in record.Fields)
                {
                    var type = contract.Fields.Single(candidate => candidate.Name == field.Name).Type;
                    assertion.Line($"{Reference("org.assertj.core.api.Assertions")}.assertThat(response.{JavaNames.Member(field.Name)}()).isEqualTo({Literal(field.Value, type)});");
                }
            }).Line(");");
        }
    }
}
