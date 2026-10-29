// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Constraints;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter;

public class when_releasing_event_occurrence : when_a_native_handler_is_constrained
{
    protected override SemanticConstraintKind Kind => SemanticConstraintKind.UniqueEventOccurrence;
    protected override bool Releases => true;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void should_not_inject_the_entity_for_the_releasing_body(bool strict) => AssertEnforced(strict, injected: false);
}
