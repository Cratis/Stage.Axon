// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Verification.for_ReactionCommandProofArtifacts;

public class when_preparing_the_native_proof : Specification
{
    IReadOnlyDictionary<string, string> _sources;

    void Because() => _sources = ReactionCommandProofArtifacts.Sources();

    [Fact] void should_embed_the_reaction_command_proof() => _sources.Keys.ShouldContainOnly("GeneratedReactionCommandTest.java");
    [Fact] void should_resolve_the_application_package() => _sources.Values.Any(source => source.Contains("__APPLICATION__", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_prove_history_is_skipped() => _sources["GeneratedReactionCommandTest.java"].ShouldContain("anEventFromBeforeTheProcessorStartedIsNotReactedTo");
    [Fact] void should_prove_the_rejection_propagates() => _sources["GeneratedReactionCommandTest.java"].ShouldContain("aCommandRejectionPropagatesOutOfTheEventHandler");
    [Fact] void should_reject_a_missing_proof_report() => Catch.Exception(() => NativeTestResults.RequireReports(ReactionCommandProofArtifacts.Application, [])).ShouldBeOfExactType<NativeVerificationFailed>();
    [Fact] void should_accept_the_proof_report() => Catch.Exception(() => NativeTestResults.RequireReports(ReactionCommandProofArtifacts.Application, ["TEST-corpusinlinejavabodies.runtime.GeneratedReactionCommandTest.xml"])).ShouldBeNull();
}
