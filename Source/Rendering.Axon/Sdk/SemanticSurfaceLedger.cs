// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Sdk;

/// <summary>
/// Explicit Screenplay 4.240.2 inventory. Phase 1 emits only scaffolding: all listed domain members
/// are deliberately deferred, not claimed as implemented. Later phases replace these dispositions
/// with rendering or construct-specific refusals. Reflection is used only by the drift spec, never here.
/// </summary>
internal static class SemanticSurfaceLedger
{
    // One row per audited semantic type. This intentionally long inventory has no inferred fallback.
    const string Members = """
        ExecutableSemanticModel Application LanguageVersion SemanticVersion Revision DeprecationDiagnostics
        LanguageVersion Major Minor
        SemanticVersion Major Minor
        SemanticRevision IsSet
        SemanticId IsSet
        EventContractId IsSet
        EventContractRevision IsValid Value
        SemanticApplication Concepts Id Modules Name Types Policies EventSources ReactionProductionMode Triggers CommandReadMode
        SemanticReactionProductionMode Atomic
        SemanticModule Features Id Name
        SemanticFeature Features Id Name Slices
        SemanticSlice Commands Constraints Events Id Kind Name Projections Queries ReadModels Specifications Reducers Reactions Captures Direction
        SemanticReducer Name ReadModel Transitions Key InitialState Result Target From
        SemanticReducerTransition EventContract RequirementId
        SemanticReducerKey EventSourceId
        SemanticReducerResult StateOrDelete
        SemanticSliceKind StateChange StateView Automation Unknown Translate
        SemanticConcept Id Name Primitive Validations Values Numbers Compliance Generation
        SemanticGeneration Kind Prefix Case Size Alphabet Count Encoding Pattern Checksum Minimum Maximum RequirementId
        SemanticConceptCompliance Personal Secret
        SemanticSecret Scope
        SemanticPersonalData Special Criminal
        SemanticCompositeType Id Name Properties
        SemanticProperty Id IsIdentifier Name Type IsGenerated
        SemanticTypeReference IsCollection IsOptional Kind Primitive Target
        SemanticTypeReferenceKind Primitive Concept CompositeType Unknown
        SemanticPrimitiveType Uuid Text WholeNumber DecimalNumber Boolean Date DateTime Unknown
        SemanticValidationRule Kind Message Property Severity Operand Name RequirementId
        SemanticValidationRuleKind NotEmpty Maximum Minimum Equal NotEqual GreaterThan GreaterThanOrEqual LessThan LessThanOrEqual Length AllGreaterThan AllGreaterThanOrEqual Matches Checksum Contains NotContains StartsWith EndsWith RulePredicate CodeValidation DecimalPlaces Unknown
        SemanticValidationSeverity Error Information Warning
        SemanticCommand Id Name Properties Validations Produces Destination Requirements Authorization Route Handler CodeValidations Response Reads
        SemanticCommandHandler RequirementId ResultVersion
        SemanticCodeValidation RequirementId
        SemanticCommandRoute Source Stream StreamId StreamIdParts
        SemanticCommandRoutePart Part Value
        SemanticEventSource Id IdentifierType Name SourceKind Streams
        SemanticEventStream Id Name StreamIdParts StreamIdType StreamKind
        SemanticStreamIdPart Name Type
        SemanticImplementationRequirement Role Owner Member Language File ContentHash Source RequirementId ContextVersion ResultVersion RequiredCapability AttachmentResolution BodySpan BodyLines QueryShape Hints IsProvisional IsAmbiguous
        SemanticAttachmentResolution Resolved UnresolvedFile Pending
        SemanticExecutionCapability ImplementationBody Command Query IdentityAllocation Projection Specification Authorization Reaction Unknown
        SemanticProducedEvent Destination EventContract Mappings Tags When Condition DestinationType Route
        SemanticEventContract Id Name StoredName Properties Tags Revision ContractId Predecessor PriorRevisions Visibility Origin
        SemanticEventRevision Predecessor Revision Properties Tags
        SemanticPropertyMapping Source TargetProperty
        SemanticStateChangeDestination Type Value
        SemanticResolvedExpression Root Source Target
        SemanticExpression Kind
        SemanticExpressionKind Resolved Refusal Unknown Value EventContext ReadPath
        SemanticRefusalExpression Member Type
        SemanticExpressionRootKind Command Event Unknown Trigger
        SemanticExpressionSourceKind Property Unknown
        SemanticEventContextExpression Type Value
        SemanticEventContextValueKind Occurred Unknown EventSourceIdentity CausedBySubject CausedByName CausedByUserName
        SemanticValueExpression Value
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
        SemanticSpecification Id Name GivenEvents GivenReadModels GivenCaller ThenEvents ThenEventsInAnyOrder ThenReadModels ThenQueries ThenErrors ThenDenied When ThenAbsentReadModels WhenAppended ThenNoEvents WhenRedelivered GivenCaptures GivenClock WhenCapture WhenClock WhenTrigger GivenGenerated ThenReturns
        SemanticSpecificationAbsentReadModel Key ReadModel
        SemanticSpecificationRedelivery Reaction Given
        SemanticSpecificationCommand Command Values EventSource GeneratedValues
        SemanticSpecificationAppend EventContract EventSource Values Route
        SemanticSpecificationEvent EventContract EventSource Values Route Unrouted
        SemanticFixtureRoute Source Stream StreamId StreamIdParts
        SemanticFixtureRoutePart Part Value
        SemanticSpecificationReadModel Key ReadModel Values Exactly
        SemanticSpecificationQueryResult Key Query Results Exactly Filters
        SemanticSpecificationError Message Code
        SemanticPropertyValue TargetProperty Value
        SemanticEventSourceIdentity Type Value
        SemanticValue Kind
        SemanticTextValue Value
        SemanticNumberValue Value
        SemanticBooleanValue Value
        SemanticArrayValue Values
        SemanticCompositeValue Properties
        SemanticValueKind Null Text Number Boolean Array Unknown Composite
        SemanticNullValue $type
        SemanticAuthenticatedCondition $type
        SemanticAnonymousCondition $type
        SemanticAuthorization $type
        SemanticCondition $type
        SemanticPolicyCondition $type
        SemanticOpaquePolicyCondition RequirementId
        SemanticNotPolicyCondition Operand
        SemanticProjectionEventSourceIdentity $type
        SemanticCaller Authenticated Claims Roles
        SemanticCallerClaim Type Value
        SemanticPolicy Condition Name
        SemanticPolicyReference Name
        SemanticClaimCondition Claim TargetKind Value
        SemanticClaimTargetKind Literal Subject Artifact
        SemanticRoleCondition Role
        SemanticLogicalAuthorization Left Operator Right
        SemanticLogicalPolicyCondition Left Operator Right
        SemanticRequirement Condition Message Severity
        SemanticComparison Left Operator Right
        SemanticComparisonOperator Equal NotEqual GreaterThan GreaterThanOrEqual LessThan LessThanOrEqual
        SemanticConditionOperand Property Value Read
        SemanticLogicalCondition Left Operator Right
        SemanticLogicalOperator And Or
        SemanticConstraint IgnoreCasing Kind Message Name ReleasedBy Scope Targets
        SemanticConstraintTarget EventContract Properties
        SemanticConstraintKind UniquePropertyValue UniqueEventOccurrence Unknown
        SemanticConstraintScope EventSequence Unknown
        SemanticApplicationTrigger Id Name Properties
        SemanticInvocation Command Mappings OnRefused
        SemanticInvocationRefusal Selector Constraint Produces
        SemanticReaction Id Name Triggers RunsAs From
        SemanticReactionTrigger Invokes Kind Produces Source Where At Every OnDayOfMonth OnDayOfWeek RequirementId
        SemanticReactionTriggerKind Event Unknown ApplicationTrigger Interval Schedule Shutdown Startup
        SemanticCapture Appends Children Id Key Map Name Nested EventsSource
        SemanticCaptureAppend EventContract EventSourceType Mappings Tags When
        SemanticCaptureChildren Appends Field IdentifiedBy Map
        SemanticCaptureNested Appends Field Map
        SemanticCaptureRecord Fields
        SemanticCaptureField Kind Name Record Records Value
        SemanticCaptureFieldKind Unknown Record Records Value
        SemanticCaptureCondition Expression Fields From Kind To
        SemanticCaptureConditionKind Unknown Added AllChanged AnyChanged Expression Removed Transition
        SemanticCaptureMap Kind Separator Source Targets Template Translations
        SemanticCaptureMapKind Unknown Split Template Value
        SemanticCaptureMapping Field TargetProperty Value
        SemanticCaptureTemplatePart Field Text
        SemanticCaptureTranslation From To
        SemanticSpecificationCapture Capture Record
        SemanticSpecificationTrigger Kind Trigger Values
        SemanticSpecificationGeneratedValue Command Property Value
        SemanticCommandResponse $type
        SemanticScalarCommandResponse Source Type
        SemanticRecordCommandResponse Fields
        SemanticCommandResponseField Name Source Type
        SemanticSpecificationResponse $type
        SemanticScalarSpecificationResponse Value
        SemanticRecordSpecificationResponse Fields
        SemanticSpecificationResponseField Name Value
        SemanticEventVisibility Private Public
        SemanticProjectionTargetKind ReadModel Event
        SemanticCaptureEventsSource Events
        SemanticReactionIdentity Kind Roles
        SemanticReactionIdentityKind System
        SemanticObserverFilter Source Stream
        SemanticCommandReadMode Protected
        SemanticDecisionRead Name ReadModel Key Presence
        SemanticDecisionReadPresence Required
        SemanticReadPathExpression Read PropertyId
        SemanticTranslationDirection Inbound Outbound
        Diagnostic Severity Code Message Location Arguments
        DiagnosticSeverity Information Warning Error
        SourceLocation Line Column Path
        DiagnosticArguments Count Keys Values
        DiagnosticArgument Kind Value
        SemanticImplementationRole CommandHandler CommandValidation ConceptValidation RulePredicate PolicyPredicate ReducerTransition QueryPerformer ConstraintPredicate ReactionEffect ValueGeneration
        SemanticAddress Kind Parts Name Application OwnerKind
        SemanticKind Application Module Feature Slice Concept CompositeType Property Command EventContract ReadModel Projection Query Specification QueryArgument Trigger Reaction Capture EventSource EventStream Unknown
        SemanticAddressPart Kind Key
        SemanticAddressPartKind Application Module Feature Slice Declaration Member Discriminator OwnerKind Generation Unknown
        ApplicationIdentity IsSet
        SemanticSourceMapEntry SemanticId Span Origin Role
        SemanticSourceSpan Document Start Length StartLine StartColumn EndLine EndColumn
        DocumentId IsSet
        SemanticIdentityOrigin Persisted LegacyBootstrap Unknown
        SemanticSourceMapRole Declaration Description Unknown
        SemanticImplementationBodySpan Start End StartLine StartColumn EndLine EndColumn
        CodeBlockSourceLine Line Column
        SemanticPerformedQueryShape ReadModelName ReadModel ReadModelAddress Cardinality Delivery Arguments Authorization
        SemanticPerformedQueryArgument Name Kind Type IsOptional
        SemanticPerformedQueryArgumentKind Key Filter
        """;

    internal static IReadOnlyDictionary<string, SemanticSurfaceDisposition> Entries { get; } = Build();

    static Dictionary<string, SemanticSurfaceDisposition> Build()
    {
        var entries = new Dictionary<string, SemanticSurfaceDisposition>(StringComparer.Ordinal);
        var deferred = new SemanticSurfaceDisposition(SemanticSurfaceDispositionKind.Ignored, "Phase 1 is scaffold-only; domain behavior is not emitted.");
        foreach (var line in Members.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var member in parts.Skip(1))
            {
                entries.Add($"{parts[0]}.{member}", deferred);
            }
        }

        entries["ExecutableSemanticModel.LanguageVersion"] = new(SemanticSurfaceDispositionKind.Rejected, "STAGE-AXON-VERSION");
        entries["ExecutableSemanticModel.SemanticVersion"] = new(SemanticSurfaceDispositionKind.Rejected, "STAGE-AXON-VERSION");
        return entries;
    }
}
