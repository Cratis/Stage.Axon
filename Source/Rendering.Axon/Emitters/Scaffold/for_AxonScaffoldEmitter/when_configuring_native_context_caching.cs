// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Scaffold.for_AxonScaffoldEmitter;

public class when_configuring_native_context_caching : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    IReadOnlyDictionary<string, string> _sources;

    void Because()
    {
        var context = new AxonEmissionContext(request, new(request.Profile));
        new AxonScaffoldEmitter().Emit(context);
        _sources = context.Artifacts.ToDictionary(artifact => artifact.RelativePath,
            artifact => Encoding.UTF8.GetString(artifact.Bytes.AsSpan()));
    }

    [Fact]
    void should_prevent_restarting_axon_entity_registration_in_cached_test_contexts() =>
        _sources["src/test/resources/spring.properties"].ShouldContain("spring.test.context.cache.pause=NEVER\n");

    [Fact]
    void should_not_change_production_context_lifecycles() => _sources
        .Any(source => source.Key.StartsWith("src/main/", StringComparison.Ordinal) &&
            source.Value.Contains("spring.test.context.cache.pause", StringComparison.Ordinal)).ShouldBeFalse();

    [Fact]
    void should_not_skip_native_tests() => _sources["pom.xml"].ShouldNotContain("skipTests");
}
