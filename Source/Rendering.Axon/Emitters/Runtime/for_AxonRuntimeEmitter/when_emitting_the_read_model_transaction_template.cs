// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Runtime.for_AxonRuntimeEmitter;

public class when_emitting_the_read_model_transaction_template : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string _source;

    void Because()
    {
        var context = new AxonEmissionContext(request, new(request.Profile));
        new AxonRuntimeEmitter().Emit(context);
        _source = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/ReadModelTransaction.java", StringComparison.Ordinal)).Bytes.AsSpan());
    }

    [Fact] void should_apply_after_commit_before_after_commit() => _source.ShouldContain("DefaultPhases.AFTER_COMMIT.order() - 1");
    [Fact] void should_release_in_do_finally() => _source.ShouldContain("context.doFinally(completed -> coordinator.release(changes))");
    [Fact] void should_apply_only_when_committed() => _source.ShouldContain("if (completed.isCommitted()) changes.commit()");
    [Fact] void should_share_changes_within_a_context() => _source.ShouldContain("context.computeResourceIfAbsent(coordinator.<K, V>resource()");
    [Fact] void should_reserve_keys_across_contexts() => _source.ShouldContain("reservations.get(key) != owner");
    [Fact] void should_bound_the_reservation_wait() => _source.ShouldContain("TimeUnit.SECONDS.toNanos(10)");
    [Fact] void should_refuse_contextless_binding() => _source.ShouldContain("Objects.requireNonNull(context,");
    [Fact] void should_not_carry_a_license_header() => _source.ShouldNotContain("Copyright");
    [Fact] void should_use_store_identity() => _source.ShouldContain("get() == key.get()");
    [Fact] void should_release_collected_stores() => _source.ShouldContain("COORDINATORS.remove(key)");
    [Fact] void should_apply_through_the_wrapped_store() => _source.ShouldContain("store.put(key, value.get())");
    [Fact] void should_restore_the_interrupt_flag() => _source.ShouldContain("Thread.currentThread().interrupt()");

    [Fact]
    void should_use_imports_not_qualified_names() =>
        _source.Split('\n').Any(line => !line.StartsWith("import ", StringComparison.Ordinal) && line.Contains("org.axonframework.", StringComparison.Ordinal)).ShouldBeFalse();
}
