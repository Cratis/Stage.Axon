// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Queries;
using Cratis.Stage.Rendering.Axon.Emitters.Scaffold;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence.for_AxonPersistenceEmitter;

public class when_emitting_a_persistent_subscription_proof : Projections.for_AxonProjectionsEmitter.given.a_view_request
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

    [Fact] void should_emit_a_surefire_sibling() => Proof.ShouldContain("class TotalPersistentSubscriptionTest");
    [Fact] void should_boot_the_subscription_fixture() => Proof.ShouldContain("start(path, SubscriptionFixture.class)");
    [Fact] void should_import_the_live_handler() => Proof.ShouldContain("@Import(LiveTotalsHandler.class)");
    [Fact] void should_open_a_real_subscription() => Proof.ShouldContain("gateway.subscriptionQuery(new LiveTotals(), LiveTotals.Result.class)");
    [Fact] void should_install_the_real_token_failure() => Proof.ShouldContain("installTokenFailure(path)");
    [Fact] void should_arm_the_token_failure() => Proof.ShouldContain("failure = Failure.TOKEN");
    [Fact] void should_assert_rollback_silence() => Proof.ShouldContain("Rollback must not notify the subscription");
    [Fact] void should_assert_committed_observation() => Proof.ShouldContain("The invalidation must observe the committed JPA row");
    [Fact] void should_require_one_invalidation_per_commit() => Proof.ShouldContain("List.of(false, true, true)");
    [Fact] void should_import_the_gateway() => Proof.ShouldContain("import org.axonframework.messaging.queryhandling.gateway.QueryGateway;");
    [Fact] void should_use_short_type_names() => Proof.ShouldNotContain("axonviews.views.counters.totals.LiveTotals.class");

    string Proof => Code("TotalPersistentSubscriptionTest");
    string Code(string name) => Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith('/' + name + ".java", StringComparison.Ordinal)));
}
