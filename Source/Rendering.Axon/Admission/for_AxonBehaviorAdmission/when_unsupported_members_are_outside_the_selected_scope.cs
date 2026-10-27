// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Admission.for_AxonBehaviorAdmission;

public class when_unsupported_members_are_outside_the_selected_scope : given.a_model_with_behaviors
{
    void Establish()
    {
        Change(slice => slice with { Kind = SemanticSliceKind.Translate, Direction = SemanticTranslationDirection.Inbound, Captures = [new(SemanticId.Create(SemanticKind.Capture, "Lines"), "Lines", "Id", [], [])] });
        request = request with { Scope = new(ArtifactRenderScopeKind.Slice, otherSlice) };
    }

    void Because() => Emit();

    [Fact] void should_not_report_the_capture() => context.Degradation.UnsupportedConstructs.ShouldBeEmpty();
}
