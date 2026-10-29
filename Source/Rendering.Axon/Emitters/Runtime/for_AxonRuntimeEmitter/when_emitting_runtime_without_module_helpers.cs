// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Runtime.for_AxonRuntimeEmitter;

public class when_emitting_runtime_without_module_helpers : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext _context;

    void Because()
    {
        _context = new AxonEmissionContext(request, new(request.Profile));
        new AxonRuntimeEmitter().Emit(_context);
    }

    [Fact] void should_not_emit_a_module_helper() => AxonRuntimePackage.ModuleTypes.Where(name => Find(name) is not null).ShouldBeEmpty();
    [Fact] void should_not_emit_the_module_helper_test() => Find("LiveSnapshotsTest").ShouldBeNull();
    [Fact] void should_not_emit_fake_contract_routes() => Find("HttpContractTest").ShouldBeNull();
    [Fact] void should_not_emit_the_read_model_helper_tests() => new[] { "ReadModelTransactionTest", "PersistentReadModelSafetyTest" }.Where(name => Find(name) is not null).ShouldBeEmpty();
    [Fact] void should_not_emit_any_persistent_helper() => _context.Artifacts.Exists(artifact => artifact.RelativePath.Contains("/runtime/persistent/", StringComparison.Ordinal)).ShouldBeFalse();

    Cratis.Stage.Contracts.Rendering.PlannedArtifact? Find(string name) => _context.Artifacts.FirstOrDefault(artifact => artifact.RelativePath.EndsWith("/runtime/" + name.Replace('.', '/') + ".java", StringComparison.Ordinal));
}
