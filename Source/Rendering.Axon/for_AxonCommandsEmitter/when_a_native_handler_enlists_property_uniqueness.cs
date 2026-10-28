// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Constraints;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter;

public class when_a_native_handler_enlists_property_uniqueness : when_a_native_handler_is_constrained
{
    string constraints;

    void Because()
    {
        var context = new AxonEmissionContext(request, new(request.Profile));
        new AxonConstraintsEmitter().Emit(context);
        constraints = Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/ModeledConstraints.java", StringComparison.Ordinal)));
    }

    [Fact] void should_expose_an_enforcing_appender() => constraints.ShouldContain("public static EventAppender enforcing(");
    [Fact] void should_route_plain_lists_through_metadata_checks() => constraints.ShouldContain("append(events, Metadata.emptyInstance());");
    [Fact] void should_handle_metadata_appends() => constraints.ShouldContain("void append(List<?> events, Metadata metadata)");
    [Fact] void should_unwrap_messages() => constraints.ShouldContain("event instanceof Message message ? message.payload() : event");
    [Fact] void should_apply_before_delegating() => (constraints.IndexOf("apply(store, payload, context);", StringComparison.Ordinal) < constraints.IndexOf("appender.append(events, metadata);", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_poison_prepare_commit() => constraints.ShouldContain("context.runOnPrepareCommit(ignored -> { throw conflict; });");
    [Fact] void should_rethrow_conflicts() => constraints.ShouldContain("catch (UniqueValueConflict conflict)");
    [Fact] void should_delegate_descriptions() => constraints.ShouldContain("appender.describeTo(descriptor);");
    [Fact]
    void should_use_imports_for_framework_types() => constraints.Split('\n')
        .Any(line => !line.TrimStart().StartsWith("import ", StringComparison.Ordinal) && !line.TrimStart().StartsWith("//", StringComparison.Ordinal) &&
            new[] { "java.", "org.axonframework.", "org.springframework.", "jakarta.", "io.cratis." }.Any(prefix => line.Contains(prefix, StringComparison.Ordinal))).ShouldBeFalse();
}
