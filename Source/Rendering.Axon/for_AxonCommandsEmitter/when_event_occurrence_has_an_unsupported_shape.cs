// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Constraints;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter;

public class when_event_occurrence_has_an_unsupported_shape : when_a_native_handler_is_constrained
{
    protected override SemanticConstraintKind Kind => SemanticConstraintKind.UniqueEventOccurrence;

    [Theory]
    [InlineData("properties")]
    [InlineData("scope")]
    [InlineData("missing-release")]
    [InlineData("missing-target")]
    [InlineData("empty")]
    public void should_not_claim_support(string shape)
    {
        var contract = located.Slice.Events.Single(candidate => candidate.Id == constraint.Targets[0].EventContract);
        var missing = SemanticId.Create(SemanticKind.EventContract, "missing-event");
        var unsupported = shape switch
        {
            "properties" => constraint with { Targets = [new(contract.Id, [contract.Properties[0].Id])] },
            "scope" => constraint with { Scope = SemanticConstraintScope.Unknown },
            "missing-release" => constraint with { ReleasedBy = [missing] },
            "missing-target" => constraint with { Targets = [new(missing, [])] },
            _ => constraint with { Targets = [] }
        };
        var context = new AxonEmissionContext(request, new(request.Profile));
        ConstraintCoverage.Supported(context, unsupported).ShouldBeFalse();
    }
}
