// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Client.for_AxonClientEmitter;

public class when_reading_the_templates : given.a_client_request
{
    string[] templates;

    void Because()
    {
        Render();
        templates = ["transport", "http-transport", "sse", "problems"];
    }

    string Source(string name) => Text("client/src/" + name + ".ts");

    [Fact] void should_not_use_dynamic_imports_enums_or_namespaces() => templates.All(name => new[] { "import(", "enum ", "namespace " }.All(word => !Source(name).Contains(word, StringComparison.Ordinal))).ShouldBeTrue();
    [Fact] void should_end_relative_imports_in_ts() => templates.All(name => Source(name).Split('\n').Where(line => line.Contains("from \"./", StringComparison.Ordinal)).All(line => line.EndsWith(".ts\";", StringComparison.Ordinal))).ShouldBeTrue();
    [Fact] void should_keep_the_transport_seam_neutral() => new[] { "fetch", "Response", "URL", "Authorization" }.All(word => !Source("transport").Contains(word, StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_not_carry_a_license_header() => templates.All(name => !Source(name).Contains("Licensed under", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_omit_credentials_on_every_request() => (Source("http-transport").Split("credentials: \"omit\"").Length - 1).ShouldEqual(3);
    [Fact] void should_send_the_problem_urns() => Source("problems").ShouldContain("\"urn:cratis:stage-axon:problem:live-capacity\"");
}
