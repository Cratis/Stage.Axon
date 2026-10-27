// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
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
    [Fact] void should_import_module_helpers_into_the_contract_tests() => Text("HttpContractTest").ShouldContain("import io.cratis.stage.axon.runtime.ValidationError;");
    [Fact] void should_import_the_module_store_into_the_persistent_store() => Text("JpaUniqueValueStore").ShouldContain("import io.cratis.stage.axon.runtime.UniqueValueStore;");

    Cratis.Stage.Contracts.Rendering.PlannedArtifact? Find(string name) => _context.Artifacts.FirstOrDefault(artifact => artifact.RelativePath.EndsWith("/runtime/" + name + ".java", StringComparison.Ordinal));

    string Text(string name) => Encoding.UTF8.GetString(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)).Bytes.AsSpan());
}
