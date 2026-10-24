// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;
using Xunit.Abstractions;

namespace Cratis.Stage.Rendering.Axon.for_AxonArtifactRenderPlanner;

public class when_measuring_blueprint_versions(ITestOutputHelper output) : Specification
{
    [Theory]
    [InlineData("orders")]
    [InlineData("ticketing")]
    [InlineData("inventory")]
    public async Task should_compile_the_authored_sample_and_measure_its_exact_pair(string sample)
    {
        var loaded = await given.blueprint_models.Load(sample);
        output.WriteLine($"{sample}: LanguageVersion={loaded.Model.LanguageVersion.Major}.{loaded.Model.LanguageVersion.Minor}; SemanticVersion={loaded.Model.SemanticVersion.Major}.{loaded.Model.SemanticVersion.Minor}");
        loaded.Model.Application.Modules.IsEmpty.ShouldBeFalse();
        foreach (var command in ScopeSelection.Locate(loaded.Model.Application).SelectMany(slice => slice.Slice.Commands))
        {
            output.WriteLine($"{command.Name}: Authorization={command.Authorization}");
            command.Authorization.ShouldNotBeNull();
        }
    }
}
