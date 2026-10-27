// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security.for_DemoIdentityConfiguration;

public class when_rendering_without_activating_demo : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    void should_never_activate_demo_in_any_artifact_or_resource(bool bearer)
    {
        var configured = request with { Profile = bearer ? AxonRendering.WithBearerTokens(request.Profile) : request.Profile };
        var plan = new AxonArtifactRenderPlanner().Plan(configured);
        plan.Artifacts.ShouldNotBeEmpty();
        foreach (var artifact in plan.Artifacts)
        {
            var source = Encoding.UTF8.GetString(artifact.Bytes.AsSpan());
            source.ShouldNotContain("spring.profiles.active");
            source.ShouldNotContain("spring.profiles.default");
            source.ShouldNotContain("on-profile: demo");
            source.ShouldNotContain("on-profile: \"demo\"");
            artifact.RelativePath.ShouldNotContain("application-demo.");
        }
        var application = Encoding.UTF8.GetString(plan.Artifacts.Single(artifact => artifact.RelativePath == "src/main/resources/application.yml").Bytes.AsSpan());
        application.ShouldNotContain("profiles:");
        var readme = Encoding.UTF8.GetString(plan.Artifacts.Single(artifact => artifact.RelativePath == "README.md").Bytes.AsSpan());
        readme.ShouldContain("-Dspring-boot.run.profiles=demo");
        readme.ShouldContain("Never activate it in production.");
        readme.ShouldContain("Requires JDK 25 or later.");
        var guide = File.ReadAllText(Path.Combine(for_AxonArtifactRenderPlanner.given.blueprint_models.Root(), "Documentation", "guides", "third-party-render-target.md"));
        guide.ShouldContain("--spring.profiles.active=demo");
        guide.ShouldContain("Never activate it in production.");
    }
}
