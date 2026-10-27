// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Admission.for_AxonBehaviorAdmission;

public class when_the_model_requests_nothing_unsupported : given.a_model_with_behaviors
{
    void Because() => Emit();

    [Fact] void should_report_nothing() => context.Degradation.UnsupportedConstructs.ShouldBeEmpty();
    [Fact] void should_emit_no_package_info() => PackageInfo().ShouldBeEmpty();
}
