// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands;

internal sealed class CommandFixtureAssertions(AxonEmissionContext context, SemanticCommand command, SemanticSpecification specification)
{
    const string ViolationException = "org.axonframework.messaging.core.interception.JSR303ViolationException";

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
            code.Reference(ViolationException);
            code.Reference("java.lang.IllegalArgumentException");
            code.Reference("java.lang.Object");
            code.Reference("java.lang.String");
            return;
        }
        ClaimResponse(code);
    }

    /// <summary>
    /// Constructs the command inside the rejection boundary: invalid concepts reject with <see cref="IllegalArgumentException"/> before dispatch,
    /// which is the modeled rejection. Any other construction failure, such as an unsupported compliance concept, still fails the test.
    /// </summary>
    internal void EmitConstruction(JavaCodeBuilder method, string input)
    {
        _code = method;
        method.Line($"{Reference("java.lang.Object")} command;").Block("try", attempt => attempt.Line($"command = {input};"))
            .Block($"catch ({Reference("java.lang.IllegalArgumentException")} error)", rejection =>
            {
                foreach (var error in specification.ThenErrors.Where(error => error.Message is not null))
                    rejection.Line($"{Reference("org.assertj.core.api.Assertions")}.assertThat({Reference("java.lang.String")}.valueOf(error.getMessage())).contains({JavaCodeBuilder.StringLiteral(error.Message!)});");
                rejection.Line("return;");
            });
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
        var violation = Reference(ViolationException);
        var assertions = Reference("org.assertj.core.api.Assertions");
        method.Block("result.exceptionSatisfies(error ->", assertion =>
        {
            assertion.Line($"{assertions}.assertThat(error).isInstanceOfAny({violation}.class, {Reference("java.lang.IllegalArgumentException")}.class);")
                .Line($"var messages = error instanceof {violation} violations ? violations.getViolations().stream().map(violation -> violation.getMessage()).toList() : {Reference("java.util.List")}.of({Reference("java.lang.String")}.valueOf(error.getMessage()));");
            foreach (var error in specification.ThenErrors.Where(error => error.Message is not null))
                assertion.Line($"{assertions}.assertThat({Reference("java.lang.String")}.join(\"; \", messages)).contains({JavaCodeBuilder.StringLiteral(error.Message!)});");
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
