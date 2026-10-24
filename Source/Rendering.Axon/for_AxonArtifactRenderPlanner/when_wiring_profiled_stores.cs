// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonArtifactRenderPlanner;

public class when_wiring_profiled_stores : given.an_axon_request
{
    string[] definitions;

    void Because()
    {
        var plan = new AxonArtifactRenderPlanner().Plan(request);
        definitions = [.. plan.Artifacts
            .Where(artifact => artifact.RelativePath.StartsWith("src/main/java/", StringComparison.Ordinal))
            .Select(artifact => Encoding.UTF8.GetString(artifact.Bytes.AsSpan()))
            .Where(code => code.Contains("UniqueValueStore uniqueValueStore(", StringComparison.Ordinal))];
    }

    [Fact] void should_define_only_the_two_profiled_store_beans() => definitions.Length.ShouldEqual(2);
    [Fact] void should_define_the_default_store_once() => definitions.Count(code => code.Contains("@org.springframework.context.annotation.Profile(\"!persistent\")", StringComparison.Ordinal)).ShouldEqual(1);
    [Fact] void should_define_the_persistent_store_once() => definitions.Count(code => code.Contains("@Profile(\"persistent\")", StringComparison.Ordinal)).ShouldEqual(1);
}
