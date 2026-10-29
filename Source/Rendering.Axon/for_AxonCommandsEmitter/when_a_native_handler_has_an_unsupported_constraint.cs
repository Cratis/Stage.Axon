// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter;

public class when_a_native_handler_has_an_unsupported_constraint : when_a_native_handler_is_constrained
{
    protected override SemanticConstraint CreateConstraint(SemanticEventContract contract, SemanticEventContract producedContract) => given.UnsupportedEnumUniqueness.For(contract);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void should_render_the_body_without_the_check_and_report_the_difference(bool strict) => AssertDegraded(strict, "enforce constraint CustomerUnique");
}
