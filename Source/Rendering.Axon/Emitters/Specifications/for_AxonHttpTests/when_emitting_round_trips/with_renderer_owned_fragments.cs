// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Specifications.for_AxonHttpTests.when_emitting_round_trips;

public class with_renderer_owned_fragments : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    const string Arrange = "authored.NativeBody.arrange(\"org.junit.jupiter.api.Assertions\");";
    const string Assertion = "result -> authored.NativeBody.verify(result, \"java.util.List\")";
    string code;
    string echo;

    void Because()
    {
        var context = new AxonEmissionContext(request, new(request.Profile));
        var ids = ScopeSelection.Select(request).SelectMany(slice => slice.Slice.Specifications).Select(spec => spec.Id).OrderBy(id => id.ToString(), StringComparer.Ordinal).Take(2).ToArray();
        var response = new AxonHttpResponse(200)
        {
            Assertions = [Assertion],
            AssertionFragments = [new(builder => $"result -> {builder.Reference("org.junit.jupiter.api.Assertions")}.assertNotNull(result)")]
        };
        AxonHttpTests.Register(context, new(ids[0], [new(new("GET", "/one"), response)], new(string.Empty, [], false))
        {
            Arrange = [Arrange],
            ArrangeFragments = [new(builder => $"var updates = application.getBean({builder.Reference("org.axonframework.messaging.queryhandling.QueryUpdateEmitter")}.class);")]
        });
        AxonHttpTests.Register(context, new(ids[1], [new(new("GET", "/two"), new(204))], new(string.Empty, [], false))
        {
            ArrangeFragments =
            [
                new(builder => $"var modeled = application.getBean({builder.Reference("example.events.QueryUpdateEmitter")}.class);", builder => builder.Reference("example.events.QueryUpdateEmitter")),
                new(builder => $"var model = application.getBean({builder.Reference("example.views.Exception")}.class);", builder => builder.Reference("example.views.Exception")),
                new(builder => $"var snapshots = {builder.Reference("java.util.List")}.of({builder.Reference("example.models.List")}.class);", builder => builder.Reference("example.models.List"))
            ]
        });
        AxonHttpTests.Emit(context);
        code = Text(context, "ModeledHttpRoundTripTest");
        echo = Text(context, "RuntimeHttpExchangeTest");
    }

    static string Text(AxonEmissionContext context, string name) => Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)).Bytes.AsSpan());

    [Fact] void should_keep_authored_arrange_verbatim() => code.ShouldContain(Arrange);
    [Fact] void should_keep_authored_assertions_verbatim() => code.ShouldContain(".andExpect(" + Assertion + ")");
    [Fact] void should_not_import_authored_type_names() => code.ShouldNotContain("import authored.NativeBody;");
    [Fact] void should_resolve_deferred_assertions() => code.ShouldContain(".andExpect(result -> Assertions.assertNotNull(result));");
    [Fact] void should_claim_modeled_names_before_any_scenario_is_rendered() => code.ShouldContain("application.getBean(org.axonframework.messaging.queryhandling.QueryUpdateEmitter.class)");
    [Fact] void should_shorten_the_modeled_type() => code.ShouldContain("application.getBean(QueryUpdateEmitter.class)");
    [Fact] void should_import_the_modeled_type() => code.ShouldContain("import example.events.QueryUpdateEmitter;");
    [Fact] void should_not_import_the_colliding_framework_type() => code.ShouldNotContain("import org.axonframework.messaging.queryhandling.QueryUpdateEmitter;");
    [Fact] void should_not_add_fragment_imports_to_the_unrelated_runtime_test() => echo.ShouldNotContain("QueryUpdateEmitter");
    [Fact] void should_keep_the_modeled_exception_import() => code.ShouldContain("import example.views.Exception;");
    [Fact] void should_keep_the_modeled_exception_short() => code.ShouldContain("application.getBean(Exception.class)");
    [Fact] void should_qualify_the_scenario_exception() => code.ShouldContain("void scenario_0() throws java.lang.Exception");
    [Fact] void should_qualify_the_perform_exception() => code.ShouldContain("perform(MockHttpServletRequestBuilder request) throws java.lang.Exception");
    [Fact] void should_keep_the_modeled_list_import() => code.ShouldContain("import example.models.List;");
    [Fact] void should_qualify_only_the_colliding_java_list() => code.ShouldContain("var snapshots = java.util.List.of(List.class);");
    [Fact] void should_not_import_the_colliding_java_list() => code.ShouldNotContain("import java.util.List;");
    [Fact] void should_leave_the_unrelated_scenario_exception_short() => echo.ShouldContain("void scenario_0() throws Exception");
    [Fact] void should_leave_the_unrelated_perform_exception_short() => echo.ShouldContain("perform(MockHttpServletRequestBuilder request) throws Exception");
}
