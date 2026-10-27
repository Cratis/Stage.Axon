// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Queries;
using Cratis.Stage.Rendering.Axon.Emitters.Scaffold;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence.for_AxonPersistenceEmitter;

public class when_emitting_normalized_persistent_proofs : Projections.for_AxonProjectionsEmitter.given.a_view_request
{
    AxonEmissionContext _context;

    void Because()
    {
        _context = new(_request, new(_request.Profile));
        new AxonScaffoldEmitter().Emit(_context);
        new AxonProjectionsEmitter().Emit(_context);
        new AxonQueriesEmitter().Emit(_context);
        new AxonPersistenceEmitter().Emit(_context);
    }

    [Fact] void should_normalize_expected_rows() => Code("TotalPersistentTest").ShouldContain("normalized(initial)");
    [Fact] void should_normalize_multi_segment_rows() => Code("TotalPersistentMultiSegmentTest").ShouldContain("normalized(afterRetry)");
    [Fact] void should_compare_the_actual_persisted_rows_after_restart() => Code("TotalPersistentTest").ShouldContain("assertEquals(persisted, new HashSet<>(store(application).all())");
    [Fact] void should_leave_flat_fields_unchanged() => Code("TotalPersistentSupport").ShouldContain("return row;");
    [Fact] void should_replace_every_proof_placeholder() => _context.Artifacts.Where(artifact => artifact.RelativePath.Contains("TotalPersistent", StringComparison.Ordinal)).All(artifact => !Text(artifact).Contains("__", StringComparison.Ordinal)).ShouldBeTrue();

    string Code(string name) => Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith('/' + name + ".java", StringComparison.Ordinal)));
}
