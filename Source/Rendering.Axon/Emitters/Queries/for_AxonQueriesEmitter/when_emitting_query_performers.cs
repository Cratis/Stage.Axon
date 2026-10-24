// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter.given;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter;

public class when_emitting_query_performers : a_view_request
{
    AxonEmissionContext _context;
    string _contract;
    string _handler;
    string _configuration;

    void Because()
    {
        _context = new(_request, new(_request.Profile));
        new AxonQueriesEmitter().Emit(_context);
        _contract = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/RankedTotalsPerformer.java", StringComparison.Ordinal)));
        _handler = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/RankedTotalsHandler.java", StringComparison.Ordinal)));
        _configuration = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/RankedTotalsPerformerConfiguration.java", StringComparison.Ordinal)));
    }

    [Fact] void should_expose_the_typed_query_and_store() => _contract.ShouldContain("perform(RankedTotals query, ReadModelStore<Object, axonviews.views.counters.totals.Total> store)");
    [Fact] void should_delegate_instead_of_silently_returning_store_rows() => _handler.ShouldContain("performer.perform(query, store)");
    [Fact] void should_fail_loudly_until_implemented() => _contract.ShouldContain("throw new UnsupportedOperationException");
    [Fact] void should_preserve_the_modeled_intent() => _contract.ShouldContain("Screenplay wanted to answer the query with its modeled performer");
    [Fact] void should_allow_a_spring_bean_to_replace_the_default() => _configuration.ShouldContain("@org.springframework.boot.autoconfigure.condition.ConditionalOnMissingBean(RankedTotalsPerformer.class)");
    [Fact] void should_warn_for_the_exact_query() => _context.Degradation.UnsupportedConstructs.Single(diagnostic => diagnostic.Artifact == _request.ImplementationRequirements.Single(requirement => requirement.Role == Cratis.Screenplay.Semantics.SemanticImplementationRole.QueryPerformer).Source.SemanticId).Code.ShouldEqual("STAGE-AXON-0401");
}
