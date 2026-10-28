// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands;

/// <summary>
/// Emits the native Axon/Spring wiring that enforces Bean Validation on dispatched commands and maps rejections to the ProblemDetails wire shape.
/// </summary>
internal static class CommandValidationInfrastructure
{
    const string ProblemPrefix = "urn:cratis:stage-axon:problem:";

    internal static void Emit(AxonEmissionContext context, IEnumerable<SemanticId> sources)
    {
        var package = context.BasePackage + ".infrastructure";
        var ids = sources.ToArray();
        context.AddJava(package, "CommandValidationConfiguration", Configuration(package), sources: ids);
        context.AddJava(package, "CommandProblemDetails", Advice(package), sources: ids);
    }

    static string Configuration(string package)
    {
        var code = new JavaCodeBuilder(package).Declare("CommandValidationConfiguration");
        var dispatch = code.Reference("org.axonframework.messaging.core.MessageDispatchInterceptor");
        var message = code.Reference("org.axonframework.messaging.commandhandling.CommandMessage");
        var interceptor = code.Reference("org.axonframework.messaging.core.interception.BeanValidationInterceptor");
        code.Line("@" + code.Reference("org.springframework.context.annotation.Configuration"))
            .Block("public class CommandValidationConfiguration", body => body.Line("@" + body.Reference("org.springframework.context.annotation.Bean"))
                .Block($"{dispatch}<{message}> beanValidationInterceptor()", method => method.Line($"return new {interceptor}<>();")));
        return code.Build();
    }

    static string Advice(string package)
    {
        var code = new JavaCodeBuilder(package).Declare("CommandProblemDetails");
        var problem = code.Reference("org.springframework.http.ProblemDetail");
        var status = code.Reference("org.springframework.http.HttpStatus");
        var handler = "@" + code.Reference("org.springframework.web.bind.annotation.ExceptionHandler");
        var violation = code.Reference("org.axonframework.messaging.core.interception.JSR303ViolationException");
        code.Line("@" + code.Reference("org.springframework.web.bind.annotation.RestControllerAdvice"))
            .Line("@" + code.Reference("org.springframework.core.annotation.Order") + "(" + code.Reference("org.springframework.core.Ordered") + ".HIGHEST_PRECEDENCE)")
            .Block("public class CommandProblemDetails", body =>
            {
                body.Line($"private static final {body.Reference("java.lang.String")} PREFIX = \"{ProblemPrefix}\";").Line()
                    .Line($"{handler}({violation}.class)")
                    .Block($"public {problem} validation({violation} exception)", method => method
                        .Line($"var problem = problem({status}.BAD_REQUEST, \"validation\", \"Command validation failed\");")
                        .Line($"{method.Reference("java.util.List")}<{method.Reference("java.util.Map")}<{method.Reference("java.lang.String")}, {method.Reference("java.lang.String")}>> errors = exception.getViolations().stream()")
                        .Line($"    .sorted({method.Reference("java.util.Comparator")}.comparing(violation -> violation.getPropertyPath().toString()))")
                        .Line($"    .map(violation -> {method.Reference("java.util.Map")}.of(\"property\", violation.getPropertyPath().toString(), \"message\", violation.getMessage()))")
                        .Line("    .toList();")
                        .Line("problem.setProperty(\"errors\", errors);")
                        .Line("return problem;")).Line()
                    .Line($"{handler}({body.Reference("java.lang.IllegalArgumentException")}.class)")
                    .Block($"public {problem} invalid({body.Reference("java.lang.IllegalArgumentException")} exception)", method => method.Line($"return problem({status}.BAD_REQUEST, \"invalid-argument\", exception.getMessage());")).Line()
                    .Line($"{handler}({body.Reference("java.lang.UnsupportedOperationException")}.class)")
                    .Block($"public {problem} unsupported({body.Reference("java.lang.UnsupportedOperationException")} exception)", method => method.Line($"return problem({status}.NOT_IMPLEMENTED, \"unsupported\", exception.getMessage());")).Line()
                    .Block($"private static {problem} problem({status} status, {body.Reference("java.lang.String")} type, {body.Reference("java.lang.String")} detail)", method => method
                        .Line($"var problem = {problem}.forStatusAndDetail(status, detail);")
                        .Line($"problem.setType({method.Reference("java.net.URI")}.create(PREFIX + type));")
                        .Line("return problem;"));
            });
        return code.Build();
    }
}
