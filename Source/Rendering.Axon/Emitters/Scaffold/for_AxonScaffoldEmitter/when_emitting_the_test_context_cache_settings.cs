// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Scaffold.for_AxonScaffoldEmitter;

public class when_emitting_the_test_context_cache_settings : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    const string Path = "src/test/resources/spring.properties";
    AxonEmissionContext _context;

    void Because()
    {
        _context = new AxonEmissionContext(request, new(request.Profile));
        new AxonScaffoldEmitter().Emit(_context);
    }

    [Fact] void should_emit_the_spring_properties_once_under_src_test_resources() => _context.Artifacts.Count(artifact => artifact.RelativePath == Path).ShouldEqual(1);
    [Fact] void should_keep_cached_contexts_running_so_axon_start_handlers_do_not_subscribe_twice() => Encoding.UTF8.GetString(_context.Artifacts.Single(artifact => artifact.RelativePath == Path).Bytes.AsSpan()).ShouldContain("spring.test.context.cache.pause=NEVER");
}
