// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Specifications.for_AxonHttpTests;

public class when_a_module_is_named_triggers : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string _test;
    string _json;
    string _package;
    string[] _routes;

    void Because()
    {
        var application = request.Model.Application with { Modules = [.. request.Model.Application.Modules.Select(module => module with { Name = "Triggers" })],
            Triggers = request.Model.Application.Triggers.Add(new(Cratis.Screenplay.Semantics.SemanticId.Create(Cratis.Screenplay.Semantics.SemanticKind.Trigger, "Wake"), "Wake", []))
        };
        var renamed = request with { Model = Cratis.Screenplay.Semantics.ExecutableSemanticModel.Create(request.Model.LanguageVersion, request.Model.SemanticVersion, application) };
        var context = new AxonEmissionContext(renamed, new(renamed.Profile));
        new Commands.AxonCommandsEmitter().Emit(context);
        AxonHttpTests.Emit(context);
        _package = context.BasePackage;
        _routes = [.. AxonHttpTestsEndpoints.For(context).Select(endpoint => endpoint.Path)];
        _test = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/HttpContractRoutesTest.java", StringComparison.Ordinal)).Bytes.AsSpan());
        _json = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("http-contract.json", StringComparison.Ordinal)).Bytes.AsSpan());
    }

    [Fact] void should_route_commands_under_the_module() => _routes.ShouldContain(route => route.StartsWith("/api/triggers/", StringComparison.Ordinal));
    [Fact] void should_keep_those_routes_in_the_expected_live_mappings() => _test.ShouldContain("\"POST /api/triggers/");
    [Fact] void should_keep_those_routes_in_the_contract_file() => _json.ShouldContain("\"path\":\"/api/triggers/");
    [Fact] void should_not_exclude_routes_by_url_prefix() => _test.ShouldNotContain("startsWith(\"/api/triggers/\")");
    [Fact] void should_exclude_the_application_trigger_endpoint_by_class() => _test.ShouldContain("\"" + _package + ".triggers.WakeEndpoint\"");
    [Fact] void should_exclude_application_triggers_by_handler_identity() => _test.ShouldContain("ClassUtils.getUserClass(handler.getValue().getBeanType()).getName()");
}
