// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonRendering;

public class when_constructing_a_model_above_the_admitted_ceiling : given.a_screen_composition_model
{
    [Fact]
    void should_be_rejected_by_the_semantic_contract()
    {
        var major = AxonRendering.NewestAdmittedSemanticVersion.Major + 1;
        Assert.Throws<InvalidSemanticContract>(() => ExecutableSemanticModel.Create(new(major, 0), new(major, 0), loaded.Model.Application));
    }
}
