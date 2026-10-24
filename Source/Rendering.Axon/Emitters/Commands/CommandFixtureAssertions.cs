// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands;

internal sealed class CommandFixtureAssertions(AxonEmissionContext context, SemanticCommand command, SemanticSpecification specification)
{
    readonly CommandJavaTypes _types = new(context.Request.Model.Application);

    internal bool CanRenderResponse() => specification.ThenReturns switch
    {
        null => true,
        SemanticScalarSpecificationResponse scalar when command.Response is SemanticScalarCommandResponse response => _types.Literal(scalar.Value, response.Type) is not null,
        SemanticRecordSpecificationResponse record when command.Response is SemanticRecordCommandResponse response => record.Fields.All(field =>
            response.Fields.FirstOrDefault(candidate => candidate.Name == field.Name) is { } target && _types.Literal(field.Value, target.Type) is not null),
        _ => false
    };

    internal void Emit(JavaCodeBuilder method, IReadOnlyList<string> expected)
    {
        if (specification.ThenDenied)
        {
            method.Line("result.exception(org.springframework.security.access.AccessDeniedException.class).noEvents();");
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

    void Errors(JavaCodeBuilder method)
    {
        method.Block("result.exceptionSatisfies(error ->", assertion =>
        {
            assertion.Line($"org.assertj.core.api.Assertions.assertThat(error).isInstanceOf({context.BasePackage}.runtime.CommandValidationException.class);")
                .Line($"var failure = ({context.BasePackage}.runtime.CommandValidationException) error;");
            foreach (var error in specification.ThenErrors.Where(error => error.Message is not null))
                assertion.Line($"org.assertj.core.api.Assertions.assertThat(failure.errors()).extracting(validationError -> validationError.message()).contains({JavaCodeBuilder.StringLiteral(error.Message!)});");
        }).Line(");").Line("result.noEvents();");
    }

    void Events(JavaCodeBuilder method, IReadOnlyList<string> expected)
    {
        if (!specification.ThenEventsInAnyOrder)
        {
            method.Line($"result.events({string.Join(", ", expected)});");
            return;
        }
        method.Line("result.eventsSatisfy(events -> org.assertj.core.api.Assertions.assertThat(events.stream().map(event -> event.payload()).toList())")
            .Line($"    .containsExactlyInAnyOrder({string.Join(", ", expected)}));");
    }

    void Response(JavaCodeBuilder method)
    {
        if (specification.ThenReturns is SemanticScalarSpecificationResponse scalar && command.Response is SemanticScalarCommandResponse response)
        {
            method.Line($"result.resultMessagePayload({_types.Literal(scalar.Value, response.Type)});");
        }
        else if (specification.ThenReturns is SemanticRecordSpecificationResponse record && command.Response is SemanticRecordCommandResponse contract)
        {
            method.Block($"result.resultMessagePayloadSatisfies({JavaNames.Type(command.Name)}Response.class, response ->", assertion =>
            {
                foreach (var field in record.Fields)
                {
                    var type = contract.Fields.Single(candidate => candidate.Name == field.Name).Type;
                    assertion.Line($"org.assertj.core.api.Assertions.assertThat(response.{JavaNames.Member(field.Name)}()).isEqualTo({_types.Literal(field.Value, type)});");
                }
            }).Line(");");
        }
    }
}
