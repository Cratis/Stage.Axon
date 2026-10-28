// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views.for_SemanticSurfaceLedger;

public class when_classifying_the_views_surface : Specification
{
    [Fact] void should_classify_live_queries_as_rendered() => SemanticSurfaceLedger.Entries["SemanticQueryDelivery.Live"].Kind.ShouldEqual(SemanticSurfaceDispositionKind.Rendered);
    [Fact] void should_account_for_conditional_persistent_read_model_storage() => SemanticSurfaceLedger.Entries["SemanticReadModel.Properties"].Kind.ShouldEqual(SemanticSurfaceDispositionKind.Warned);
    [Fact]
    void should_register_the_writerless_code()
    {
        SemanticSurfaceLedger.Entries["SemanticReadModel.Properties"].Codes.ShouldContain("STAGE-AXON-0309");
        SemanticSurfaceLedger.Entries["SemanticReadModel.Properties"].Detail.ShouldContain("STAGE-AXON-0309");
    }

    [Fact] void should_register_the_persistence_warning_code() => SemanticSurfaceLedger.Entries["SemanticReadModel.Properties"].Detail.ShouldContain("STAGE-AXON-0308");
    [Fact] void should_explain_retained_generation_refusal() => SemanticSurfaceLedger.Entries["SemanticReadModel.Properties"].Detail.ShouldContain("Retained data without compatible generation metadata is refused");
    [Fact] void should_not_promise_automatic_recovery() => SemanticSurfaceLedger.Entries["SemanticReadModel.Properties"].Detail.ShouldContain("automatic replay and legacy adoption are not performed");
    [Fact] void should_explain_conditional_view_refusal() => SemanticSurfaceLedger.Entries["SemanticReadModel.Properties"].Detail.ShouldContain("warns for unsupported shapes during default rendering and refuses them under strict rendering or persistent startup");
    [Fact] void should_classify_children_as_rendered() => SemanticSurfaceLedger.Entries["SemanticProjectionScope.Children"].Kind.ShouldEqual(SemanticSurfaceDispositionKind.Rendered);
    [Fact] void should_classify_reducer_bodies_as_warned() => SemanticSurfaceLedger.Entries["SemanticReducerTransition.RequirementId"].Detail.ShouldContain("STAGE-AXON-0306");
    [Fact] void should_not_claim_query_expectations_execute() => SemanticSurfaceLedger.Entries["SemanticQueryFilterValue.Value"].Kind.ShouldEqual(SemanticSurfaceDispositionKind.Warned);
    [Fact] void should_explain_legacy_cardinality_ignoring() => SemanticSurfaceLedger.Entries["SemanticAffectedInstance.Cardinality"].Detail.ShouldContain("Legacy cardinality metadata");
    [Fact] void should_not_claim_unknown_variants_render() => SemanticSurfaceLedger.Entries["SemanticProjectionOperation.Unknown"].Kind.ShouldEqual(SemanticSurfaceDispositionKind.Ignored);
}
