// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Sdk;

internal static partial class SemanticSurfaceLedger
{
    const string ViewsMembers = """
        SemanticReducer Name ReadModel Transitions Key InitialState Result Target From
        SemanticReducerTransition EventContract RequirementId
        SemanticReducerKey EventSourceId
        SemanticReducerResult StateOrDelete
        SemanticReadModel Id Name Properties
        SemanticProjection Id Name ReadModel Transitions Scope Target
        SemanticProjectionTransition AffectedInstance EventContract Mappings
        SemanticAffectedInstance Cardinality Key
        AffectedInstanceCardinality One Unknown Many ZeroOrOne
        SemanticKeyedQuery Argument Cardinality Delivery Id KeyProperty Name ReadModel Authorization Filters KeyParts
        SemanticQueryKeyPart Id Name Type KeyProperty
        SemanticQueryFilter Id Name Type Property
        SemanticQueryFilterValue Filter Value
        SemanticReadModelQueryArgument Id Name Type
        SemanticQueryCardinality ZeroOrOne Many Unknown One
        SemanticQueryDelivery Snapshot Live Unknown
        SemanticProjectionScope Children From Joins Every JoinRemovals Nested Removals
        SemanticProjectionChildren IdentifiedBy Property Scope
        SemanticProjectionCompositeKey Parts Type
        SemanticProjectionEventContextValue Path
        SemanticProjectionEventProperty Path
        SemanticProjectionEvery IncludeChildren Mappings SubscribesToAllEvents
        SemanticProjectionFrom EventContract Key Mappings ParentKey
        SemanticProjectionJoin EventContract Mappings On Key
        SemanticProjectionJoinRemoval EventContract Key
        SemanticProjectionKey Kind
        SemanticProjectionKeyKind Value Unknown Composite Parts
        SemanticProjectionPartsKey Parts
        SemanticProjectionKeyPart Property Value
        SemanticProjectionLiteral Value
        SemanticProjectionMapping Operation Source Target Factor
        SemanticProjectionNested Property Scope
        SemanticProjectionOperation Set Add Subtract Increment Decrement Clear Unknown
        SemanticProjectionRemoval EventContract Key ParentKey
        SemanticProjectionValue Kind
        SemanticProjectionValueKey Value
        SemanticProjectionValueKind EventProperty EventSourceIdentity Literal Unknown EventContext
        """;

    static void ClassifyViews(LedgerBuilder rows)
    {
        const string persistent = "Conditional persistent support for admitted flat views with generation metadata and guarded pooled processors. Event-context values, nested targets and reducer-only models remain refused under STAGE-AXON-0308: their implementation is present but held back pending the multi-segment duplicate-apply proof. STAGE-AXON-0308 warns for unsupported shapes during default rendering and refuses them under strict rendering or persistent startup. Retained data without compatible generation metadata is refused; automatic replay and legacy adoption are not performed.";
        rows.Warned(["STAGE-AXON-0308"], persistent, "SemanticReadModel.Properties");
        rows.Warned(["STAGE-AXON-0308", "STAGE-AXON-0307"], persistent + " " + ProjectionMappingDetail, "SemanticProjectionEventContextValue.Path");
        rows.Warned(["STAGE-AXON-0306", "STAGE-AXON-0308"], "Conditional LF-only, SHA-256-matched pure Java transition bodies are placed verbatim. STAGE-AXON-0306 defaults return current; events are ignored until a supported body is authored. InitialState is advisory; current is null on first delivery. Reducer-only persistence implementation is present but held back under STAGE-AXON-0308 pending the multi-segment duplicate-apply proof, including fully placed transitions without event filters. Unplaced transitions and unsupported target/from forms stay outside persistence and degrade explicitly; dependent specifications remain disabled unless fully supported.", "SemanticReducer.Transitions SemanticReducer.InitialState SemanticReducer.Target SemanticReducer.From SemanticReducerTransition.RequirementId");
        rows.Ignored(IgnoredReason.Identity, "Declaration identity metadata; the generated artifact is attributed to its owning query.", "SemanticQueryKeyPart.Id SemanticQueryFilter.Id SemanticReadModelQueryArgument.Id");
        rows.Ignored(IgnoredReason.Advisory, "The generated reducer interface is named after its read model, not the modeled reducer name.", "SemanticReducer.Name");
        rows.Ignored(IgnoredReason.Advisory, "Projection nomination is not consumed; Axon emits its own typed event projector.", "SemanticProjection.Target");
        rows.Ignored(IgnoredReason.Advisory, "Legacy cardinality metadata does not select bulk behavior: the projector applies one event to its modeled key.", "SemanticAffectedInstance.Cardinality AffectedInstanceCardinality.One AffectedInstanceCardinality.Many AffectedInstanceCardinality.ZeroOrOne");
        rows.Ignored(IgnoredReason.UnknownSentinel, "Legacy cardinality metadata does not select bulk behavior: the projector applies one event to its modeled key.", "AffectedInstanceCardinality.Unknown");
        rows.Ignored(IgnoredReason.UnknownSentinel, "Unknown sentinel has no supported Axon interpretation; it is not an additional executable variant.", "SemanticQueryCardinality.Unknown SemanticQueryDelivery.Unknown SemanticProjectionKeyKind.Unknown SemanticProjectionOperation.Unknown SemanticProjectionValueKind.Unknown");
    }
}
