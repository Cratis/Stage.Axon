// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Verification.for_CorpusApplications;

public class when_preserving_application_admission_and_identity : Specification
{
    readonly Dictionary<string, LoadedSemanticModel> _models = new(StringComparer.Ordinal);
    readonly Dictionary<string, ArtifactRenderPlan> _plans = new(StringComparer.Ordinal);
    IReadOnlyList<(string Name, string Root)> _applications;
    CanonicalCorpusInputs _inputs;

    async Task Establish()
    {
        var repository = given.the_repository.Root();
        _inputs = CanonicalCorpusInputs.Create(repository);
        _applications = CorpusApplications.Find(repository, _inputs.Root);
        foreach (var (name, root) in _applications)
        {
            var applicationName = "Corpus" + string.Concat(name.Split('-').Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
            var compilation = File.Exists(root)
                ? await SemanticModelLoader.LoadAsync(Path.GetDirectoryName(root)!, [Path.GetFileName(root)], null, applicationName)
                : await SemanticModelLoader.LoadAsync(root, [], null, applicationName);
            compilation.Success.ShouldBeTrue();
            compilation.Loaded!.Model.Application.Name.ShouldEqual(applicationName);
            _models.Add(name, compilation.Loaded);
        }
    }

    void Because()
    {
        foreach (var (name, loaded) in _models)
        {
            _plans.Add(name, name switch
            {
                IngressProofModel.Application => IngressProofModel.Plan(loaded),
                IngressProofModel.PersistentApplication => IngressProofModel.PersistentPlan(loaded),
                _ => AxonRendering.PlanFrom(loaded, new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id))
            });
        }
    }

    void Destroy() => _inputs?.Dispose();

    [Fact]
    void should_keep_all_twenty_seven_application_names() => _applications.Select(application => application.Name).SequenceEqual<string>(
    [
        "administration", "approvals", "bookings", "catalog", "contracts", "crm", "customer-portal", "hr", "inventory", "invoicing", "orders", "projects", "ticketing", "vendors", "work-orders",
        "sample-navigation", "sample-templates", "sample-custom-packages", "views-combined", "view-composite-keys", "view-reducers", "view-scopes", "view-views", "ingress-invariants", "ingress-persistent", "inline-java-policies", "client-proof"
    ]).ShouldBeTrue();

    [Fact] void should_keep_native_report_application_names() => CorpusApplications.Find(given.the_repository.Root()).Select(application => application.Name).SequenceEqual(_applications.Select(application => application.Name)).ShouldBeTrue();
    [Fact] void should_compile_every_application() => _models.Count.ShouldEqual(27);
    [Fact] void should_plan_every_application_without_refusals() => (_plans.Count == 27 && _plans.Values.All(plan => plan.Success && plan.Refusals.IsEmpty)).ShouldBeTrue();
    [Fact] void should_use_the_composed_custom_package_root() => _applications.Single(application => application.Name == "sample-custom-packages").Root.ShouldEqual(_inputs.Root);

    [Fact] void should_bind_proof_facts_to_the_authenticated_caller() => ProofSource("RegisterAccountHandler").ShouldContain("Caller.current().identity()");
    [Fact] void should_enlist_proof_uniqueness_in_the_append_context() => ProofSource("ModeledConstraints").ShouldContain("store.assign(context,");
    [Fact] void should_release_proof_claims_with_the_generated_close_command() => ProofSource("ModeledConstraints").ShouldContain("store.releaseOwner(context,");
    [Fact] void should_protect_the_proof_command_handler() => ProofSource("RegisterAccountHandler").ShouldContain("@PreAuthorize(\"hasRole('Writer')\")");
    [Fact] void should_enroll_the_proof_decision_before_validation() => ProofSource("ConsumeAccountHandler").ShouldContain("if (state == null || !state.decisionRead0()) throw new CommandValidationException");
    [Fact] void should_refuse_the_unsupported_cross_stream_proof() => ProofSource("CloseOtherAccountHandler").ShouldContain("throw new UnsupportedOperationException(\"STAGE-AXON-0106:");
    [Fact] void should_not_append_in_the_unsupported_proof() => ProofSource("CloseOtherAccountHandler").ShouldNotContain("appender.append(");
    [Fact] void should_emit_the_persistent_gateway_race_base() => ProofSource("PersistentUniqueConcurrencyTest").ShouldContain("abstract CompletableFuture<Void> register(UUID owner, String value)");
    [Fact] void should_preserve_the_scoped_memory_proof_view() => ProofSource("ActiveAccountsStoreConfiguration").ShouldContain("new PersistentReadModelSafety(");
    [Fact] void should_not_render_views_for_the_persistent_proofs() => _plans[IngressProofModel.PersistentApplication].Artifacts.Any(artifact => artifact.RelativePath.Contains("/accounts/registry/active/", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_warn_about_persistent_proof_views() => _plans[IngressProofModel.PersistentApplication].Diagnostics.Any(diagnostic => diagnostic.Code == "STAGE-AXON-0308").ShouldBeFalse();
    [Fact] void should_enforce_persistent_protected_reads() => PersistentProofSource("ConsumeAccountHandler").ShouldContain("if (state == null || !state.decisionRead0()) throw new CommandValidationException");
    [Fact] void should_preserve_persistent_read_removals() => PersistentProofSource("ConsumeAccountStream").ShouldContain("decisionRead0 = false;");
    [Fact] void should_preserve_persistent_read_creation() => PersistentProofSource("ConsumeAccountStream").ShouldContain("decisionRead0 = true;");
    [Fact] void should_enlist_persistent_uniqueness() => PersistentProofSource("ModeledConstraints").ShouldContain("store.assign(context,");
    [Fact] void should_release_persistent_claims() => PersistentProofSource("ModeledConstraints").ShouldContain("store.releaseOwner(context,");

    string ProofSource(string name) => Source(IngressProofModel.Application, name);
    string PersistentProofSource(string name) => Source(IngressProofModel.PersistentApplication, name);
    string Source(string application, string name) => Encoding.UTF8.GetString(_plans[application].Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)).Bytes.AsSpan());

    [Fact]
    public async Task should_keep_custom_application_identity_across_owned_input_directories()
    {
        using var other = CanonicalCorpusInputs.Create(given.the_repository.Root());
        var compilation = await SemanticModelLoader.LoadAsync(other.Root, [], null, "CorpusSampleCustomPackages");
        compilation.Success.ShouldBeTrue();
        compilation.Loaded!.Model.Application.Id.ShouldEqual(_models["sample-custom-packages"].Model.Application.Id);
    }
}
