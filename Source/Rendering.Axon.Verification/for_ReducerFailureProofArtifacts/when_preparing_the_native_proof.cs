// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Verification.for_ReducerFailureProofArtifacts;

public class when_preparing_the_native_proof : Specification
{
    static readonly string[] _applications = [ReducerFailureProofArtifacts.Application, ReducerFailureProofArtifacts.PersistentApplication];
    ArtifactRenderPlan _plan;

    static string Text(PlannedArtifact artifact) => Encoding.UTF8.GetString(artifact.Bytes.AsSpan());

    async Task Establish()
    {
        var repository = for_CorpusApplications.given.the_repository.Root();
        var compilation = await SemanticModelLoader.LoadAsync(Path.Combine(repository, "Source/Rendering.Axon.Verification/Corpus"), ["reducer-failure.play"], null, "CorpusReducerFailure");
        if (!compilation.Success) throw new NativeVerificationFailed(string.Join("; ", compilation.Diagnostics.Select(diagnostic => diagnostic.Code + ": " + diagnostic.Message)));
        _plan = AxonRendering.PlanFrom(compilation.Loaded, new(ArtifactRenderScopeKind.Application, compilation.Loaded.Model.Application.Id), default, CorpusRenderer.Profile);
    }

    [Fact] void should_render_with_warnings_but_no_refusals() => (_plan.Success && _plan.Refusals.IsEmpty && !_plan.Diagnostics.IsEmpty).ShouldBeTrue();
    [Fact] void should_not_emit_the_quarantined_projector() => _plan.Artifacts.Any(artifact => artifact.RelativePath.EndsWith("/QuarantinedTotalReducerProjector.java", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_emit_any_successful_fallback() => Java().ShouldNotContain("return current;");
    [Fact] void should_use_an_unavailable_store_even_in_the_persistent_profile() => Source("QuarantinedTotalStoreConfiguration").ShouldNotContain("@Profile");
    [Fact] void should_not_create_a_readable_substitute() => Source("QuarantinedTotalStoreConfiguration").ShouldNotContain("InMemoryReadModelStore");
    [Fact] void should_refuse_direct_modeled_queries() => Source("QuarantinedTotalsHandler").ShouldContain("throw new IllegalStateException");
    [Fact] void should_refuse_direct_live_queries() => Source("LiveQuarantinedTotalsHandler").ShouldContain("throw new IllegalStateException");
    [Fact] void should_refuse_http_before_opening_a_subscription() => Source("LiveQuarantinedTotalsEndpoint").ShouldNotContain("streams.open(");
    [Fact] void should_refuse_http_without_exposing_a_reason() => Source("QuarantinedTotalsEndpoint").ShouldContain("throw new ResponseStatusException(HttpStatus.SERVICE_UNAVAILABLE);");
    [Fact] void should_preserve_endpoint_authorization() => Source("ProtectedQuarantinedTotalsEndpoint").ShouldContain("@PreAuthorize");
    [Fact] void should_preserve_handler_authorization() => Source("ProtectedQuarantinedTotalsHandler").ShouldContain("@PreAuthorize");
    [Fact] void should_not_execute_an_opaque_performer() => Source("OpaqueQuarantinedTotalsHandler").ShouldNotContain("performer.perform(");
    [Fact] void should_refuse_the_unrelated_opaque_performer_too() => Source("UnrelatedOpaqueTotalsHandler").ShouldContain("throw new IllegalStateException");
    [Fact] void should_not_place_the_unrelated_performer_body() => Source("UnrelatedOpaqueTotalsPerformer").ShouldNotContain("throw new AssertionError");
    [Fact] void should_explain_collateral_refusal_in_comments() => Source("UnrelatedOpaqueTotalsHandler").ShouldContain("dependencies are not modeled");
    [Fact] void should_explain_collateral_refusal_in_the_summary() => Text(_plan.Artifacts.Single(artifact => artifact.RelativePath == "UNSUPPORTED.md")).ShouldContain("includes unrelated performers");
    [Fact] void should_keep_the_healthy_modeled_query_working() => Source("HealthyTotalsHandler").ShouldContain("store.all().stream()");
    [Fact] void should_keep_the_healthy_projector() => Source("HealthyProjectionProjector").ShouldContain("store.update(");
    [Fact] void should_keep_the_healthy_model_proofs() => Source("HealthyTotalViewSpecificationsTest").ShouldContain("new HealthyProjectionProjector(");
    [Fact] void should_emit_executable_refusal_proofs() => Source("QuarantinedTotalQuarantineSpecificationsTest").ShouldContain("assertThrows");
    [Fact] void should_not_emit_new_disabled_tests() => Java().ShouldNotContain("@Disabled");
    [Fact] void should_not_emit_tests_referencing_a_nonexistent_projector() => Java().ShouldNotContain("new QuarantinedTotalReducerProjector(");
    [Fact] void should_not_emit_successful_reduction_proofs_for_quarantine() => Java().ShouldNotContain("new QuarantinedTotalReducer(");

    [Fact]
    void should_require_the_native_report_for_both_applications()
    {
        foreach (var application in _applications)
        {
            Assert.Throws<NativeVerificationFailed>(() => NativeTestResults.RequireReports(application, []));
            NativeTestResults.RequireReports(application, ["TEST.example.GeneratedReducerFailureTest.xml"]);
        }
    }

    [Fact]
    void should_prepare_a_real_native_proof_for_each_profile()
    {
        var source = ReducerFailureProofArtifacts.Sources("proof", persistent: true).Single().Value;
        source.ShouldContain("@ActiveProfiles(\"persistent\")");
        source.ShouldContain("retainedRowsCannotLeakOrBeMutated");
        source.ShouldContain("anUnaffectedModeledQueryWorksButAnUnrelatedOpaquePerformerIsRefused");
        source.ShouldContain("assertUnavailableSubscription()");
        source.ShouldContain("snapshotAndSseRefuseWithoutExposingRowsOrBypassingAuthorization");
        source.ShouldNotContain("__APPLICATION__");
        ReducerFailureProofArtifacts.Sources("proof", persistent: false).Single().Value.ShouldContain("@ActiveProfiles(\"default\")");
    }

    string Java() => string.Join('\n', _plan.Artifacts.Where(artifact => artifact.RelativePath.EndsWith(".java", StringComparison.Ordinal)).Select(Text));
    string Source(string name) => Text(_plan.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)));
}
