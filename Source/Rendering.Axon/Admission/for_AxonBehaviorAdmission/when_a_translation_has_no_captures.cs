// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Admission.for_AxonBehaviorAdmission;

public class when_a_translation_has_no_captures : given.a_model_with_behaviors
{
    void Establish() => Change(slice => slice with { Kind = SemanticSliceKind.Translate, Direction = SemanticTranslationDirection.Inbound });

    void Because() => Emit();

    [Fact] void should_report_0601_on_the_slice() => context.Degradation.UnsupportedConstructs.Single().Artifact.ShouldEqual(changedSlice);
    [Fact] void should_name_the_direction() => PackageInfo().Single().ShouldContain("translate inbound");
}
