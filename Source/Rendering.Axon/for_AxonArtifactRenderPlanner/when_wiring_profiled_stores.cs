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

    [Fact] void should_generate_no_store_bean() => definitions.Length.ShouldEqual(0);
    [Fact] void should_define_the_default_store_once_in_the_runtime_module() => File.ReadAllText(Path.Combine(given.blueprint_models.Root(), "Source", "Runtime", "src", "main", "java", "io", "cratis", "stage", "axon", "runtime", "InMemoryUniqueConfiguration.java")).ShouldNotContain("@Profile");
}
