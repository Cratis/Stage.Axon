// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter;

public class when_rendering_native_validation_infrastructure : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string configuration;
    string advice;

    void Because()
    {
        var context = new AxonEmissionContext(request, new(request.Profile));
        new AxonCommandsEmitter().Emit(context);
        configuration = Text(context, "CommandValidationConfiguration.java");
        advice = Text(context, "CommandProblemDetails.java");
    }

    [Fact] void should_register_the_native_bean_validation_interceptor() => configuration.ShouldContain("return new BeanValidationInterceptor<>();");
    [Fact] void should_declare_the_dispatch_interceptor_type() => configuration.ShouldContain("MessageDispatchInterceptor<CommandMessage> beanValidationInterceptor()");
    [Fact] void should_run_before_other_advice() => advice.ShouldContain("@Order(Ordered.HIGHEST_PRECEDENCE)");
    [Fact] void should_map_violations_to_the_validation_problem() => advice.ShouldContain("@ExceptionHandler(JSR303ViolationException.class)");
    [Fact] void should_keep_the_problem_type_prefix() => advice.ShouldContain("urn:cratis:stage-axon:problem:");
    [Fact] void should_sort_the_property_errors_by_wire_name() => advice.ShouldContain("Comparator.comparing(error -> error.get(\"property\"))");
    [Fact] void should_report_wire_names_for_the_property_paths() => advice.ShouldContain("\"property\", wireName(violation.getRootBeanClass(), violation.getPropertyPath().toString())");
    [Fact] void should_translate_each_path_segment_through_the_json_property_names() => advice.ShouldContain("field.getAnnotation(JsonProperty.class)");
    [Fact] void should_split_the_path_into_segments() => advice.ShouldContain("path.split(\"\\\\.\", -1)");
    [Fact] void should_keep_collection_indices() => advice.ShouldContain("var suffix = bracket < 0 ? \"\" : segment.substring(bracket);");
    [Fact] void should_follow_nested_and_collection_content_types() => advice.ShouldContain("content(field.getGenericType())");
    [Fact] void should_import_the_wire_name_annotation() => advice.ShouldContain("import com.fasterxml.jackson.annotation.JsonProperty;");
    [Fact] void should_map_domain_rejections_to_bad_request() => advice.ShouldContain("return problem(HttpStatus.BAD_REQUEST, \"invalid-argument\", exception.getMessage());");
    [Fact] void should_not_depend_on_the_runtime_module() => (configuration + advice).ShouldNotContain("io.cratis");

    static string Text(AxonEmissionContext context, string file) =>
        Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/infrastructure/" + file, StringComparison.Ordinal)).Bytes.AsSpan());
}
