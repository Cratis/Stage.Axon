// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Constraints;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter;

public class when_enlisting_event_occurrence : when_a_native_handler_is_constrained
{
    protected override SemanticConstraintKind Kind => SemanticConstraintKind.UniqueEventOccurrence;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void should_enforce_the_native_body(bool strict) => AssertEnforced(strict);

    [Fact] void should_assert_the_occurrence_without_an_owner_argument() => Emit(false).Code.ShouldContain("customerUnique.assertCustomerUniqueUnique();");
}
