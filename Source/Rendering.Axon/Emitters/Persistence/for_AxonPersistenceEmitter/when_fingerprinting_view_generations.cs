// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence.for_AxonPersistenceEmitter;

public class when_fingerprinting_view_generations : Projections.for_AxonProjectionsEmitter.given.a_view_request
{
    string _original;
    string _reordered;
    string _changed;

    void Because()
    {
        var eventSource = PlannedArtifact.CreateText("src/main/java/sample/Event.java", "record Event(long value) {}", [ReadModelId("Total")]);
        var modelSource = PlannedArtifact.CreateText("src/main/java/sample/Model.java", "record Model(long value) {}", [ReadModelId("Total")]);
        _original = Fingerprint(eventSource, modelSource);
        _reordered = Fingerprint(modelSource, eventSource);
        _changed = Fingerprint(modelSource, PlannedArtifact.CreateText(eventSource.RelativePath, "record Event(String value) {}", [ReadModelId("Total")]));
    }

    [Fact] void should_be_independent_of_artifact_iteration_order() => _original.ShouldEqual(_reordered);
    [Fact] void should_change_with_the_generated_contract() => _original.ShouldNotEqual(_changed);
    [Fact] void should_emit_a_nonempty_format_versioned_fingerprint() => Assert.Matches("FINGERPRINT = \"[0-9A-F]{64}\";", _original);
    [Fact]
    void should_refuse_the_previous_routing_generation()
    {
        const string contract = "src/main/java/sample/Event.java\nrecord Event(long value) {}\nsrc/main/java/sample/Model.java\nrecord Model(long value) {}";
        var previous = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("flat-view-json-v1\n" + contract)));
        _original.ShouldNotContain(previous);
    }

    string Fingerprint(params PlannedArtifact[] sources)
    {
        var context = new AxonEmissionContext(_request, new(_request.Profile));
        context.Artifacts.AddRange(sources);
        new AxonPersistenceEmitter().Emit(context);
        return Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/PersistentViewConfiguration.java", StringComparison.Ordinal)).Bytes.AsSpan());
    }
}
