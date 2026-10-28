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
    [Fact] void should_sort_the_property_errors() => advice.ShouldContain("Comparator.comparing(violation -> violation.getPropertyPath().toString())");
    [Fact] void should_map_domain_rejections_to_bad_request() => advice.ShouldContain("return problem(HttpStatus.BAD_REQUEST, \"invalid-argument\", exception.getMessage());");
    [Fact] void should_not_depend_on_the_runtime_module() => (configuration + advice).ShouldNotContain("io.cratis");

    static string Text(AxonEmissionContext context, string file) =>
        Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/infrastructure/" + file, StringComparison.Ordinal)).Bytes.AsSpan());
}
