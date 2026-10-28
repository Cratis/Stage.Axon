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
    string constraints;

    protected override SemanticConstraintKind Kind => SemanticConstraintKind.UniqueEventOccurrence;
    protected override bool Releases => true;

    void Because()
    {
        var context = new AxonEmissionContext(request, new(request.Profile));
        new AxonConstraintsEmitter().Emit(context);
        constraints = Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/ModeledConstraints.java", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void should_enforce_the_releasing_body(bool strict) => AssertEnforced(strict);

    [Fact] void should_release_by_the_primary_source() => constraints.ShouldContain($"if (event instanceof OrderMoved fact) store.releaseOwner(context, \"{ConstraintSlice()}/CustomerUnique\", fact.orderId());");

    SemanticId ConstraintSlice() => ScopeSelection.Locate(request.Model.Application).Single(slice => slice.Slice.Constraints.Any(candidate => candidate.Name == constraint.Name)).Slice.Id;
}
