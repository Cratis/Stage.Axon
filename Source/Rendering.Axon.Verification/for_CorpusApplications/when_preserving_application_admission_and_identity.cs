// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
            _plans.Add(name, AxonRendering.PlanFrom(loaded, new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id)));
        }
    }

    void Destroy() => _inputs?.Dispose();

    [Fact]
    void should_keep_all_twenty_three_application_names() => _applications.Select(application => application.Name).SequenceEqual<string>(
    [
        "administration", "approvals", "bookings", "catalog", "contracts", "crm", "customer-portal", "hr", "inventory", "invoicing", "orders", "projects", "ticketing", "vendors", "work-orders",
        "sample-navigation", "sample-templates", "sample-custom-packages", "views-combined", "view-composite-keys", "view-reducers", "view-scopes", "view-views"
    ]).ShouldBeTrue();

    [Fact] void should_keep_native_report_application_names() => CorpusApplications.Find(given.the_repository.Root()).Select(application => application.Name).SequenceEqual(_applications.Select(application => application.Name)).ShouldBeTrue();
    [Fact] void should_compile_every_application() => _models.Count.ShouldEqual(23);
    [Fact] void should_plan_every_application_without_refusals() => (_plans.Count == 23 && _plans.Values.All(plan => plan.Success && plan.Refusals.IsEmpty)).ShouldBeTrue();
    [Fact] void should_use_the_composed_custom_package_root() => _applications.Single(application => application.Name == "sample-custom-packages").Root.ShouldEqual(_inputs.Root);

    [Fact]
    public async Task should_keep_custom_application_identity_across_owned_input_directories()
    {
        using var other = CanonicalCorpusInputs.Create(given.the_repository.Root());
        var compilation = await SemanticModelLoader.LoadAsync(other.Root, [], null, "CorpusSampleCustomPackages");
        compilation.Success.ShouldBeTrue();
        compilation.Loaded!.Model.Application.Id.ShouldEqual(_models["sample-custom-packages"].Model.Application.Id);
    }
}
