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
    [Fact]
    void should_import_the_normalization_collection_types()
    {
        Code("TotalPersistentSupport").ShouldContain("import java.util.Set;");
        Code("TotalPersistentSupport").ShouldContain("import java.util.Collection;");
        Code("TotalPersistentSupport").ShouldContain("import java.util.stream.Collectors;");
    }

    [Fact]
    void should_use_short_names_in_normalization()
    {
        Code("TotalPersistentSupport").ShouldContain("static Set<");
        Code("TotalPersistentSupport").ShouldContain("normalized(Collection<");
        Code("TotalPersistentSupport").ShouldContain(".collect(Collectors.toSet())");
    }

    [Fact]
    void should_import_the_persisted_row_set_type()
    {
        Code("TotalPersistentTest").ShouldContain("import java.util.Set;");
        Code("TotalPersistentTest").ShouldNotContain("java.util.Set<");
    }

    [Fact] void should_import_the_state_supplier() => Code("TotalPersistentSupport").ShouldContain("import java.util.function.Supplier;");
    [Fact] void should_accept_a_state_supplier() => Code("TotalPersistentSupport").ShouldContain("static void await(BooleanSupplier condition, Supplier<String> state) throws Exception");
    [Fact] void should_keep_the_condition_only_overload() => Code("TotalPersistentSupport").ShouldContain("await(condition, () -> \"\");");
    [Fact] void should_report_state_only_on_timeout() => Code("TotalPersistentSupport").ShouldContain("assertTrue(condition.getAsBoolean(), () -> \"Persistent processor/query condition timed out\" + state.get())");

    [Theory]
    [InlineData("Test", 5)]
    [InlineData("MultiSegmentTest", 3)]
    [InlineData("LegacyTest", 1)]
    void should_report_state_for_every_row_wait(string suffix, int count)
    {
        var waits = Code("TotalPersistent" + suffix).Split(';').Where(statement => statement.Contains("await(() ->", StringComparison.Ordinal) && statement.Contains("store(application)", StringComparison.Ordinal)).ToArray();
        waits.Length.ShouldEqual(count);
        foreach (var wait in waits)
        {
            wait.ShouldContain("() -> \": expected rows: \" + normalized(");
            wait.ShouldContain("\", actual rows: \" + normalized(store(application).all())");
            wait.ShouldContain("\", count: \" + count(application)");
            wait.ShouldContain("\", tokens: \" + tokens(application)");
        }
    }

    [Fact] void should_leave_flat_fields_unchanged() => Code("TotalPersistentSupport").ShouldContain("return row;");
    [Fact] void should_replace_every_proof_placeholder() => _context.Artifacts.Where(artifact => artifact.RelativePath.Contains("TotalPersistent", StringComparison.Ordinal)).All(artifact => !Text(artifact).Contains("__", StringComparison.Ordinal)).ShouldBeTrue();

    string Code(string name) => Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith('/' + name + ".java", StringComparison.Ordinal)));
}
