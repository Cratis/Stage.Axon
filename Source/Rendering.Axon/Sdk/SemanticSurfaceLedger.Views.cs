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
}
