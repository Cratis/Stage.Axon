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

    static SemanticSurfaceLedger()
    {
        var entries = (Dictionary<string, SemanticSurfaceDisposition>)Entries;
        foreach (var line in ViewsMembers.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var member in parts.Skip(1))
                entries[$"{parts[0]}.{member}"] = new(SemanticSurfaceDispositionKind.Rendered, "Axon immutable view, event projector or typed query artifact; unsupported forms degrade at their owner.");
        }

        SetViews(entries, SemanticSurfaceDispositionKind.Warned, "Conditional persistent support for admitted flat views with generation metadata and guarded pooled processors. Event-context values, nested targets and reducer-only models remain refused under STAGE-AXON-0308: their implementation is present but held back pending the multi-segment duplicate-apply proof. STAGE-AXON-0308 warns for unsupported shapes during default rendering and refuses them under strict rendering or persistent startup. Retained data without compatible generation metadata is refused; automatic replay and legacy adoption are not performed.", "SemanticReadModel.Properties SemanticProjectionEventContextValue.Path");
        SetViews(entries, SemanticSurfaceDispositionKind.Warned, "Conditional LF-only, SHA-256-matched pure Java transition bodies are placed verbatim. STAGE-AXON-0306 defaults return current; events are ignored until a supported body is authored. InitialState is advisory; current is null on first delivery. Reducer-only persistence implementation is present but held back under STAGE-AXON-0308 pending the multi-segment duplicate-apply proof, including fully placed transitions without event filters. Unplaced transitions and unsupported target/from forms stay outside persistence and degrade explicitly; dependent specifications remain disabled unless fully supported.", "SemanticReducer.Transitions SemanticReducer.InitialState SemanticReducer.Target SemanticReducer.From SemanticReducerTransition.RequirementId");
        SetViews(entries, SemanticSurfaceDispositionKind.Ignored, "Declaration identity metadata; the generated artifact is attributed to its owning query.", "SemanticQueryKeyPart.Id SemanticQueryFilter.Id SemanticReadModelQueryArgument.Id");
        SetViews(entries, SemanticSurfaceDispositionKind.Ignored, "The generated reducer interface is named after its read model, not the modeled reducer name.", "SemanticReducer.Name");
        SetViews(entries, SemanticSurfaceDispositionKind.Ignored, "Projection nomination is not consumed; Axon emits its own typed event projector.", "SemanticProjection.Target");
        SetViews(entries, SemanticSurfaceDispositionKind.Ignored, "Legacy cardinality metadata does not select bulk behavior: the projector applies one event to its modeled key.", "SemanticAffectedInstance.Cardinality AffectedInstanceCardinality.One AffectedInstanceCardinality.Many AffectedInstanceCardinality.ZeroOrOne AffectedInstanceCardinality.Unknown");
        SetViews(entries, SemanticSurfaceDispositionKind.Ignored, "Unknown sentinel has no supported Axon interpretation; it is not an additional executable variant.", "SemanticQueryCardinality.Unknown SemanticQueryDelivery.Unknown SemanticProjectionKeyKind.Unknown SemanticProjectionOperation.Unknown SemanticProjectionValueKind.Unknown");
        ClassifyConditions(entries);
    }

    static void SetViews(Dictionary<string, SemanticSurfaceDisposition> entries, SemanticSurfaceDispositionKind kind, string detail, string members)
    {
        foreach (var member in members.Split(' ', StringSplitOptions.RemoveEmptyEntries)) entries[member] = new(kind, detail);
    }
}
