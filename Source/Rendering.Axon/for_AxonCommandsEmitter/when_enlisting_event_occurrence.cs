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
    string constraints;

    protected override SemanticConstraintKind Kind => SemanticConstraintKind.UniqueEventOccurrence;

    void Because()
    {
        var context = new AxonEmissionContext(request, new(request.Profile));
        new AxonConstraintsEmitter().Emit(context);
        constraints = Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/ModeledConstraints.java", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void should_enforce_the_native_body(bool strict) => AssertEnforced(strict);

    [Fact] void should_use_the_primary_source_as_owner() => constraints.ShouldContain($"if (event instanceof OrderPlaced fact) store.occurOnce(context, \"{located.Slice.Id}/CustomerUnique\", fact.orderId(), \"Constraint violated: CustomerUnique\");");
    [Fact] void should_not_assign_property_values() => constraints.ShouldNotContain("store.assign(");
    [Fact]
    void should_use_imports() => constraints.Split('\n').Any(line => !line.TrimStart().StartsWith("import ", StringComparison.Ordinal) &&
        !line.TrimStart().StartsWith("//", StringComparison.Ordinal) &&
        new[] { "java.", "org.axonframework.", "org.springframework.", "jakarta.", "io.cratis." }.Any(prefix => line.Contains(prefix, StringComparison.Ordinal))).ShouldBeFalse();
}
