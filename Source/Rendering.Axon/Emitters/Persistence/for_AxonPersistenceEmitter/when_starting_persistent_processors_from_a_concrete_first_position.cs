// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Queries;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence.for_AxonPersistenceEmitter;

public class when_starting_persistent_processors_from_a_concrete_first_position : Projections.for_AxonProjectionsEmitter.given.a_view_request
{
    AxonEmissionContext _context;
    string _configuration;

    void Because()
    {
        _context = new(_request, new(_request.Profile));
        new AxonProjectionsEmitter().Emit(_context);
        new AxonQueriesEmitter().Emit(_context);
        new AxonPersistenceEmitter().Emit(_context);
        _configuration = Encoding.UTF8.GetString(_context.Artifacts
            .Single(artifact => artifact.RelativePath.EndsWith("/PersistentViewConfiguration.java", StringComparison.Ordinal)).Bytes.AsSpan());
    }

    [Fact] void should_start_every_segment_from_the_first_token() => CountOf("config.initialToken(source -> source.firstToken(null));").ShouldEqual(CountOf("EventProcessorDefinition.pooledStreamingMatching("));

    [Fact] void should_emit_persistent_processors() => CountOf("EventProcessorDefinition.pooledStreamingMatching(").ShouldBeGreaterThan(0);

    [Fact] void should_not_wrap_the_initial_token_in_a_replay_token() => _configuration.ShouldNotContain("ReplayToken::");

    int CountOf(string text) => _configuration.Split(text).Length - 1;
}
