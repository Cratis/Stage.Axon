// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views.for_SemanticSurfaceLedger;

public class when_classifying_the_views_surface : Specification
{
    [Fact] void should_classify_live_queries_as_rendered() => SemanticSurfaceLedger.Entries["SemanticQueryDelivery.Live"].Kind.ShouldEqual(SemanticSurfaceDispositionKind.Rendered);
    [Fact] void should_account_for_volatile_read_model_storage() => SemanticSurfaceLedger.Entries["SemanticReadModel.Properties"].Kind.ShouldEqual(SemanticSurfaceDispositionKind.Warned);
    [Fact] void should_register_the_persistence_warning_code() => SemanticSurfaceLedger.Entries["SemanticReadModel.Properties"].Detail.ShouldContain("STAGE-AXON-0308");
    [Fact] void should_classify_children_as_rendered() => SemanticSurfaceLedger.Entries["SemanticProjectionScope.Children"].Kind.ShouldEqual(SemanticSurfaceDispositionKind.Rendered);
    [Fact] void should_classify_reducer_bodies_as_warned() => SemanticSurfaceLedger.Entries["SemanticReducerTransition.RequirementId"].Detail.ShouldContain("STAGE-AXON-0306");
    [Fact] void should_not_claim_query_expectations_execute() => SemanticSurfaceLedger.Entries["SemanticQueryFilterValue.Value"].Kind.ShouldEqual(SemanticSurfaceDispositionKind.Rejected);
    [Fact] void should_explain_legacy_cardinality_ignoring() => SemanticSurfaceLedger.Entries["SemanticAffectedInstance.Cardinality"].Detail.ShouldContain("Legacy cardinality metadata");
    [Fact] void should_not_claim_unknown_variants_render() => SemanticSurfaceLedger.Entries["SemanticProjectionOperation.Unknown"].Kind.ShouldEqual(SemanticSurfaceDispositionKind.Ignored);
}
