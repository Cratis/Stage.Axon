// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Sdk.for_SemanticSurfaceLedger;

public class when_auditing_native_implementation_support : Specification
{
    [Theory]
    [InlineData("SemanticCommand.Handler")]
    [InlineData("SemanticCommandHandler.RequirementId")]
    [InlineData("SemanticReactionTrigger.RequirementId")]
    [InlineData("SemanticImplementationRole.QueryPerformer")]
    [InlineData("SemanticImplementationRequirement.ContentHash")]
    public void should_describe_conditional_native_support_without_claiming_universal_rendering(string member)
    {
        var entry = SemanticSurfaceLedger.Entries[member];
        entry.Kind.ShouldEqual(SemanticSurfaceDispositionKind.Warned);
        entry.Detail.ShouldContain("LF-only");
        entry.Detail.ShouldContain("SHA-256");
        entry.Detail.ShouldContain("STAGE-AXON-0403");
    }

    [Theory]
    [InlineData("SemanticReducerTransition.RequirementId")]
    [InlineData("SemanticImplementationRole.ReducerTransition")]
    public void should_retain_truthful_reducer_degradation(string member)
    {
        var entry = SemanticSurfaceLedger.Entries[member];
        entry.Kind.ShouldEqual(SemanticSurfaceDispositionKind.Warned);
        entry.Detail.ShouldContain("STAGE-AXON-0306");
        entry.Detail.ShouldContain("disabled");
    }
}
