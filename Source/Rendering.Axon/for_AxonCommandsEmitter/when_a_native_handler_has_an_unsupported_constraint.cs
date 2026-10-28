// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter;

public class when_a_native_handler_has_an_unsupported_constraint : when_a_native_handler_is_constrained
{
    protected override SemanticConstraintKind Kind => SemanticConstraintKind.UniqueEventOccurrence;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void should_fail_closed_without_consuming_the_body(bool strict) => AssertRefused(strict, "enforce or release modeled uniqueness in the append unit of work");
}
