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

    static void WireName(JavaCodeBuilder method)
    {
        var json = method.Reference("com.fasterxml.jackson.annotation.JsonProperty");
        method.Line($"var result = new {method.Reference("java.lang.StringBuilder")}();").Line($"{method.Reference("java.lang.reflect.Type")} type = root;")
            .Block("for (var segment : path.split(\"\\\\.\", -1))", loop => loop
                .Line("int bracket = segment.indexOf('[');")
                .Line("var member = bracket < 0 ? segment : segment.substring(0, bracket);")
                .Line("var suffix = bracket < 0 ? \"\" : segment.substring(bracket);")
                .Line("var field = field(type, member);")
                .Line($"var name = field == null ? null : field.getAnnotation({json}.class);")
                .Line($"type = field == null ? {method.Reference("java.lang.Object")}.class : content(field.getGenericType());")
                .Line("if (name != null && !name.value().isEmpty()) member = name.value();")
                .Line("if (!result.isEmpty()) result.append('.');")
                .Line("result.append(member).append(suffix);"))
            .Line("return result.toString();");
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
                        .Line($"    .map(violation -> {method.Reference("java.util.Map")}.of(\"property\", wireName(violation.getRootBeanClass(), violation.getPropertyPath().toString()), \"message\", violation.getMessage()))")
                        .Line($"    .sorted({method.Reference("java.util.Comparator")}.comparing(error -> error.get(\"property\")))")
                        .Line("    .toList();")
                        .Line("problem.setProperty(\"errors\", errors);")
                        .Line("return problem;")).Line()
                    .Line($"{handler}({body.Reference("java.lang.IllegalArgumentException")}.class)")
                    .Block($"public {problem} invalid({body.Reference("java.lang.IllegalArgumentException")} exception)", method => method.Line($"return problem({status}.BAD_REQUEST, \"invalid-argument\", exception.getMessage());")).Line()
                    .Line($"{handler}({body.Reference("java.lang.UnsupportedOperationException")}.class)")
                    .Block($"public {problem} unsupported({body.Reference("java.lang.UnsupportedOperationException")} exception)", method => method.Line($"return problem({status}.NOT_IMPLEMENTED, \"unsupported\", exception.getMessage());")).Line()
                    .Block($"private static {body.Reference("java.lang.String")} wireName({body.Reference("java.lang.Class")}<?> root, {body.Reference("java.lang.String")} path)", WireName).Line()
                    .Block($"private static {body.Reference("java.lang.reflect.Field")} field({body.Reference("java.lang.reflect.Type")} type, {body.Reference("java.lang.String")} name)", method => method
                        .Line($"var raw = type instanceof {method.Reference("java.lang.Class")}<?> plain ? plain : type instanceof {method.Reference("java.lang.reflect.ParameterizedType")} parameterized ? ({method.Reference("java.lang.Class")}<?>) parameterized.getRawType() : null;")
                        .Line("if (raw == null) return null;")
                        .Block("try", attempt => attempt.Line("return raw.getDeclaredField(name);"))
                        .Block($"catch ({method.Reference("java.lang.NoSuchFieldException")} missing)", none => none.Line("return null;"))).Line()
                    .Block($"private static {body.Reference("java.lang.reflect.Type")} content({body.Reference("java.lang.reflect.Type")} type)", method => method
                        .Line($"if (type instanceof {method.Reference("java.lang.Class")}<?> array && array.isArray()) return array.getComponentType();")
                        .Line($"if (type instanceof {method.Reference("java.lang.reflect.ParameterizedType")} parameterized && parameterized.getActualTypeArguments().length > 0 && parameterized.getRawType() instanceof {method.Reference("java.lang.Class")}<?> raw && ({method.Reference("java.lang.Iterable")}.class.isAssignableFrom(raw) || {method.Reference("java.util.Map")}.class.isAssignableFrom(raw))) return parameterized.getActualTypeArguments()[parameterized.getActualTypeArguments().length - 1];")
                        .Line("return type;")).Line()
                    .Block($"private static {problem} problem({status} status, {body.Reference("java.lang.String")} type, {body.Reference("java.lang.String")} detail)", method => method
                        .Line($"var problem = {problem}.forStatusAndDetail(status, detail);")
                        .Line($"problem.setType({method.Reference("java.net.URI")}.create(PREFIX + type));")
                        .Line("return problem;"));
            });
        return code.Build();
    }
}
