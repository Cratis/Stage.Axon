// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_emitting_projector_rollback_proofs : Persistence.for_PersistentViewAdmission.given.a_flat_view
{
    AxonEmissionContext _context;
    string _scoped;
    string _reducer;

    void Because()
    {
        _context = Render(slice => slice);
        _scoped = Code(_context, "BoardProjectionProjectorRollbackTest");
        _reducer = Code(_context, "RebuiltTotalReducerProjectorRollbackTest");
    }

    [Fact] void should_emit_the_scoped_proof() => _scoped.ShouldContain("class BoardProjectionProjectorRollbackTest");
    [Fact] void should_emit_the_reducer_proof() => _reducer.ShouldContain("class RebuiltTotalReducerProjectorRollbackTest");
    [Fact] void should_not_emit_a_flat_proof() => _context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("/TotalProjectionProjectorRollbackTest.java", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_inject_the_failure_in_commit() => _scoped.ShouldContain("context.runOnCommit(committing -> { throw new IllegalStateException(\"rollback-injected\"); });");
    [Fact] void should_assert_the_injected_failure() => _scoped.ShouldContain("rollback-injected");
    [Fact] void should_drive_the_scoped_handler() => _scoped.ShouldContain("projector.handle(");
    [Fact] void should_drive_the_reducer_handler() => _reducer.ShouldContain("projector.handle(");
    [Fact] void should_carry_the_message_in_the_context() => _scoped.ShouldContain("Message.addToContext(");
    [Fact] void should_cover_the_join_before_parent_case() => _scoped.ShouldContain("childAndJoinBeforeParent");
    [Fact] void should_prove_recovery_from_a_projection_that_fails_part_way() => _scoped.ShouldContain("AfterAFailedProjection() {");
    [Fact] void should_fail_the_projection_through_the_store() => _scoped.ShouldContain("throw new IllegalStateException(\"projection-failure-injected\");");
    [Fact] void should_prove_a_concurrent_rollback() => _scoped.ShouldContain("concurrent(false);");
    [Fact] void should_prove_a_concurrent_commit() => _scoped.ShouldContain("concurrent(true);");
    [Fact] void should_not_disable_a_proof() => _scoped.ShouldNotContain("@Disabled");
    [Fact] void should_not_carry_a_license_header() => _scoped.ShouldNotContain("Copyright (c) Cratis");
}
