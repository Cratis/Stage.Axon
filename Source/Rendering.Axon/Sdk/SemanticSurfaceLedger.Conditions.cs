// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Sdk;

internal static partial class SemanticSurfaceLedger
{
    const string DegradationModes = " On the unsupported branch, default rendering warns and retains artifacts; strict rendering errors and refuses the plan. Generated execution limitations below are distinct from render refusal.";

    static void ClassifyConditions(Dictionary<string, SemanticSurfaceDisposition> entries)
    {
        ClassifyCommands(entries);
        ClassifyDomainConditions(entries);
        ClassifyReactions(entries);
        ClassifyViewConditions(entries);
        ClassifySpecifications(entries);
        Warn(
            entries,
            "STAGE-AXON-VERSION: measured language/semantic pairs 2.0/2.0, 6.0/6.0, 10.0/10.0 and 25.0/25.0 are admitted; other pairs degrade.",
            "ExecutableSemanticModel.LanguageVersion ExecutableSemanticModel.SemanticVersion");
    }

    static void ClassifyCommands(Dictionary<string, SemanticSurfaceDisposition> entries)
    {
        Warn(
            entries,
            "STAGE-AXON-0106: empty reads succeed. Required reads enroll only with a non-generated source, no command/produced routes, key equal to the source input id and exactly one ReadModel projection. Its single-level scope needs nonempty From, event-source identity keys without parents, matching primary tag/type and disjoint create/remove events; no joins, children, nested, every or join removals. Failed enrollment emits a throwing handler at the command owner. Enrolled reads emit presence guards; absent state is runtime validation failure, not renderer degradation. This is narrow event-sourced enrollment, not general DCB support.",
            "SemanticCommand.Reads SemanticDecisionRead.Name SemanticDecisionRead.ReadModel SemanticDecisionRead.Key SemanticDecisionRead.Presence SemanticDecisionReadPresence.Required SemanticCommandReadMode.Protected SemanticApplication.CommandReadMode");
        Warn(
            entries,
            "STAGE-AXON-0801: UniquePropertyValue with EventSequence, nonempty resolved targets/properties and primitive or scalar non-enum concept properties is supported. Unsupported constraints warn at the slice and affected commands fail closed; ReleasedBy participates in applicability and release enrollment. A native command with any applicable constraint is blocked even when that constraint is supported. Unrelated commands are unaffected.",
            "SemanticSlice.Constraints SemanticConstraint.Kind SemanticConstraint.Scope SemanticConstraint.Targets SemanticConstraint.ReleasedBy SemanticConstraintTarget.EventContract SemanticConstraintTarget.Properties SemanticConstraintKind.UniquePropertyValue SemanticConstraintKind.UniqueEventOccurrence SemanticConstraintKind.Unknown SemanticConstraintScope.EventSequence SemanticConstraintScope.Unknown SemanticCommand.Produces SemanticCommand.Handler");
        SetViews(
            entries,
            SemanticSurfaceDispositionKind.Rendered,
            "For supported constraints, Name forms the store key, Message supplies the violation text and IgnoreCasing is passed to uniqueness assignment; these fields do not select constraint admission.",
            "SemanticConstraint.Name SemanticConstraint.Message SemanticConstraint.IgnoreCasing");
        Warn(
            entries,
            "STAGE-AXON-0110: declarative commands require a primary source when producing events, a computable response and resolvable identity/property mappings for every append and condition. Failure emits a throwing handler without partial modeled appends. Supported mappings/responses render; a native handler bypasses these declarative checks, not constraint/read gates.",
            "SemanticCommand.Destination SemanticCommand.Produces SemanticCommand.Response SemanticProducedEvent.EventContract SemanticProducedEvent.Destination SemanticProducedEvent.DestinationType SemanticProducedEvent.Mappings SemanticProducedEvent.When SemanticProducedEvent.Condition SemanticCommandResponse.$type SemanticScalarCommandResponse.Source SemanticScalarCommandResponse.Type SemanticRecordCommandResponse.Fields SemanticCommandResponseField.Name SemanticCommandResponseField.Source SemanticCommandResponseField.Type");
        Warn(
            entries,
            "STAGE-AXON-0107: command input and concept-derived rules and requirements render when property/predicate/condition resolves. Unresolved properties, null predicates, unsupported conditions or attached validation code emit validation.reject and runtime CommandValidationException. Requirement Message is used on the supported path; Severity is not enforced by command requirement/rule emission.",
            "SemanticCommand.Validations SemanticCommand.Requirements SemanticCommand.CodeValidations SemanticCodeValidation.RequirementId SemanticRequirement.Condition SemanticRequirement.Message SemanticRequirement.Severity SemanticValidationRule.Property SemanticValidationRule.Kind SemanticValidationRule.Operand SemanticValidationRule.Severity");
        Warn(entries, "STAGE-AXON-APPEND: nonempty additional tags warn only on the reached successful declarative append path; empty tags do not. Primary-tagged events still append; native and earlier throwing branches do not execute this metadata diagnostic.", "SemanticProducedEvent.Tags");
        Warn(
            entries,
            "STAGE-AXON-ROUTING: command or produced routes warn only on the reached successful declarative append path; only the primary source tag is retained, not source/substream metadata. No route gives no diagnostic. Protected routed reads can fail earlier with 0106; native bodies bypass this metadata check.",
            "SemanticCommand.Route SemanticProducedEvent.Route SemanticCommandRoute.Source SemanticCommandRoute.Stream SemanticCommandRoute.StreamId SemanticCommandRoute.StreamIdParts SemanticCommandRoutePart.Part SemanticCommandRoutePart.Value");
    }

    static void ClassifyDomainConditions(Dictionary<string, SemanticSurfaceDisposition> entries)
    {
        Warn(
            entries,
            "STAGE-AXON-GENERATION: concept create supports uuid kind with UUID primitive; generated command properties require noncollection UUID and uuid concept strategy. Allocated source identities support text/UUID subject to runtime-id/concept-number checks. Unsupported preparation/create emits a throwing method. Other generation parameters are not evidence of implemented strategies; deterministic fixture overrides are separate.",
            "SemanticConcept.Generation SemanticGeneration.Kind SemanticProperty.IsGenerated SemanticCommand.Destination");
        Warn(
            entries,
            "STAGE-AXON-IMPL: scalar concept Error rules with supported predicates render. Unsupported predicates report intent and throw for nonnull values on Error rules; non-Error severity reports unsupported intent without that runtime throw. Command-derived concept rules separately use 0107.",
            "SemanticConcept.Validations SemanticValidationRule.Kind SemanticValidationRule.Operand SemanticValidationRule.Severity");
        Warn(
            entries,
            "STAGE-AXON-AUTHORIZATION: null authorization emits no annotation; anonymous/authenticated/role/not and supported logical policies render. Missing/cyclic policies, unsupported claim conditions or operators warn at the command/query owner and emit denyAll, retaining default artifacts.",
            "SemanticCommand.Authorization SemanticKeyedQuery.Authorization SemanticApplication.Policies SemanticPolicy.Condition SemanticPolicy.Name SemanticPolicyReference.Name SemanticLogicalAuthorization.Left SemanticLogicalAuthorization.Operator SemanticLogicalAuthorization.Right SemanticLogicalPolicyCondition.Left SemanticLogicalPolicyCondition.Operator SemanticLogicalPolicyCondition.Right SemanticNotPolicyCondition.Operand SemanticClaimCondition.Claim SemanticClaimCondition.TargetKind SemanticClaimCondition.Value");
        Warn(
            entries,
            "STAGE-AXON-COMPLIANCE: scalar concepts with Personal or Secret metadata warn because protection at rest is not configured; the constructor throws. Ordinary scalar concepts do not take this guard. The enum emission path does not apply this scalar guard; no protection semantics are claimed there.",
            "SemanticConcept.Compliance SemanticConceptCompliance.Personal SemanticConceptCompliance.Secret SemanticSecret.Scope SemanticPersonalData.Special SemanticPersonalData.Criminal");
        Warn(
            entries,
            "STAGE-AXON-EVENT-TAGS: event records retain the primary event-source tag. Distinct contract tags and identifier property names other than that primary key warn at the event owner and are omitted; the default record is still emitted. A sole primary identifier does not degrade.",
            "SemanticEventContract.Tags SemanticProperty.IsIdentifier");
        Warn(
            entries,
            "STAGE-AXON-EVOLUTION: nonempty PriorRevisions or nonnull Predecessor warns at the current contract owner and requires an upcaster. Only the current version-annotated record is emitted, not historical records or an automatic upcaster.",
            "SemanticEventContract.PriorRevisions SemanticEventContract.Predecessor");
        SetViews(entries, SemanticSurfaceDispositionKind.Rendered, "Current contract revision supplies the event version annotation; history/predecessor separately degrades with STAGE-AXON-EVOLUTION.", "SemanticEventContract.Revision");
        Warn(
            entries,
            "STAGE-AXON-TYPE: TypeFor renders supported primitive/concept/composite references and collections. Unknown or unresolved references fall back to Object (or List<Object>) and warn at type.Target, which need not be a declaration. Primitive/Claim helpers alone do not guarantee a diagnostic.",
            "SemanticProperty.Type SemanticTypeReference.Kind SemanticTypeReference.Primitive SemanticTypeReference.Target SemanticTypeReference.IsCollection");
    }

    static void ClassifyReactions(Dictionary<string, SemanticSurfaceDisposition> entries)
    {
        Warn(
            entries,
            "STAGE-AXON-0504: reaction registration requires every trigger to prepare with supported identity, input, annotation, shape, effect and guard, with no duplicate event sources. Native effects must be standalone; declarative effects permit one invocation OR one production; reaction.From is unsupported. One failed trigger prevents registration of the whole reaction, though artifacts remain in default output.",
            "SemanticReaction.Triggers SemanticReaction.From SemanticReactionTrigger.Kind SemanticReactionTrigger.Source SemanticReactionTrigger.Where SemanticReactionTrigger.Invokes SemanticReactionTrigger.Produces SemanticReactionTrigger.RequirementId");
        Warn(
            entries,
            "STAGE-AXON-0505: an invocation of an identity-requiring command without RunsAs warns and also fails trigger preparation with 0504. Declared System can succeed subject to other gates; public commands need no identity. Unsupported identity kinds fail with 0504, not 0505; roles do not independently establish trusted identity.",
            "SemanticReaction.RunsAs SemanticReactionIdentity.Kind SemanticReactionIdentity.Roles SemanticReactionTrigger.Invokes");
        Warn(
            entries,
            "STAGE-AXON-0507: successfully emitted Interval/Schedule methods use process-local UTC Spring scheduling, not durable scheduling; interval boundaries start with the process. Other trigger kinds do not emit this diagnostic, nor do schedules rejected before emission.",
            "SemanticReactionTrigger.Kind SemanticReactionTrigger.At SemanticReactionTrigger.Every SemanticReactionTrigger.OnDayOfMonth SemanticReactionTrigger.OnDayOfWeek SemanticReactionTriggerKind.Interval SemanticReactionTriggerKind.Schedule");
    }

    static void ClassifyViewConditions(Dictionary<string, SemanticSurfaceDisposition> entries)
    {
        Warn(
            entries,
            "STAGE-AXON-0305: on the flat projection path, a null key conversion warns at the projection owner and skips only that From update. Supported identity, literal, event-property, composite and parts keys render; scoped projection emission is a separate path.",
            "SemanticProjection.Transitions SemanticProjection.Scope SemanticAffectedInstance.Key SemanticProjectionFrom.Key SemanticProjectionKey.Kind SemanticProjectionValueKey.Value SemanticProjectionCompositeKey.Parts SemanticProjectionCompositeKey.Type SemanticProjectionPartsKey.Parts SemanticProjectionKeyPart.Property SemanticProjectionKeyPart.Value");
        Warn(
            entries,
            "STAGE-AXON-0307: supported Clear/Set and numeric Add/Subtract/Increment/Decrement mappings with resolvable operands/factors render. Unresolved target paths or value expressions warn and skip that mapping, retaining supported siblings. Flat targets resolve to one model property; scoped mappings report at owner /mapping.",
            "SemanticProjectionTransition.Mappings SemanticProjectionFrom.Mappings SemanticProjectionJoin.Mappings SemanticProjectionEvery.Mappings SemanticProjectionMapping.Target SemanticProjectionMapping.Source SemanticProjectionMapping.Operation SemanticProjectionMapping.Factor SemanticProjectionValue.Kind SemanticProjectionEventProperty.Path SemanticProjectionEventContextValue.Path SemanticProjectionLiteral.Value");
        Warn(
            entries,
            "STAGE-AXON-0401: in the performed query branch, unresolved argument types skip the query; an unresolved native performer emits a typed throwing extension interface. A valid Java slot is consumed and renders its raw body. Ordinary live delivery alone does not cause this diagnostic; typed query keys and filters remain supported.",
            "SemanticImplementationRole.QueryPerformer SemanticImplementationRequirement.QueryShape SemanticPerformedQueryShape.ReadModel SemanticPerformedQueryShape.ReadModelName SemanticPerformedQueryShape.ReadModelAddress SemanticPerformedQueryShape.Arguments SemanticPerformedQueryShape.Cardinality SemanticPerformedQueryShape.Delivery SemanticPerformedQueryShape.Authorization SemanticPerformedQueryArgument.Name SemanticPerformedQueryArgument.Kind SemanticPerformedQueryArgument.Type SemanticPerformedQueryArgument.IsOptional SemanticKeyedQuery.Id SemanticKeyedQuery.ReadModel SemanticKeyedQuery.Argument SemanticKeyedQuery.KeyParts SemanticKeyedQuery.Filters");
    }

    static void ClassifySpecifications(Dictionary<string, SemanticSurfaceDisposition> entries)
    {
        Warn(
            entries,
            "STAGE-AXON-0112: command specifications selected by When.Command are enabled for supported typed fixtures, including clock and current-command generation. Command degradation, historical constrained state, read-model/capture fixtures, other-command generation, missing identity, stable error codes, unrenderable responses, routed/unrouted occurrences or incomplete values disable the test and warn at the specification, not the command.",
            "SemanticSpecification.When SemanticSpecification.GivenEvents SemanticSpecification.ThenEvents SemanticSpecification.GivenReadModels SemanticSpecification.ThenReadModels SemanticSpecification.ThenQueries SemanticSpecification.ThenAbsentReadModels SemanticSpecification.GivenCaptures SemanticSpecification.GivenGenerated SemanticSpecification.ThenErrors SemanticSpecification.ThenReturns SemanticSpecificationError.Code SemanticSpecificationEvent.Route SemanticSpecificationEvent.Unrouted SemanticSpecificationGeneratedValue.Command SemanticSpecificationGeneratedValue.Property SemanticSpecificationGeneratedValue.Value");
        Warn(
            entries,
            "STAGE-AXON-0501: view specifications render enabled tests when the legacy fixture or native plan supports their seed/action/assertions; otherwise tests are disabled with a warning at the specification owner. Query expectations are not supported by either path; query filter declarations themselves still render.",
            "SemanticQueryFilterValue.Filter SemanticQueryFilterValue.Value SemanticSpecification.ThenQueries SemanticSpecification.GivenReadModels SemanticSpecification.ThenReadModels SemanticSpecification.WhenAppended SemanticSpecification.ThenAbsentReadModels SemanticSpecificationReadModel.Exactly");
        Warn(
            entries,
            "STAGE-AXON-0504: reaction specifications enable supported application-trigger event assertions; unsupported prior state, generation, query/read-model/rejection/response fixtures or incomplete values disable the test at the specification owner. CanEmit checks all application reaction triggers: all must be ApplicationTrigger and exactly one must match the source, not just the selected trigger.",
            "SemanticSpecification.WhenTrigger SemanticSpecification.GivenEvents SemanticSpecification.GivenReadModels SemanticSpecification.GivenCaptures SemanticSpecification.GivenGenerated SemanticSpecification.ThenEvents SemanticSpecification.ThenReadModels SemanticSpecification.ThenQueries SemanticSpecification.ThenReturns SemanticSpecification.ThenDenied SemanticSpecification.ThenErrors SemanticSpecification.ThenAbsentReadModels");
        Warn(
            entries,
            "STAGE-AXON-SPEC: unclaimed specifications emit disabled placeholders and warn at the specification owner; a field alone does not establish whether an emitter claims the specification. Nonempty ThenInvocations independently warns even on already claimed tests; calls and values are not verified.",
            "SemanticSlice.Specifications SemanticSpecification.WhenCapture SemanticSpecification.WhenClock SemanticSpecification.WhenRedelivered SemanticSpecification.ThenInvocations SemanticSpecificationInvocation.Command SemanticSpecificationInvocation.Values");
        SetViews(entries, SemanticSurfaceDispositionKind.Rendered, "GivenClock supplies the command fixture clock when used and the reaction fixture clock; it does not establish clock-driven action or durable scheduling support.", "SemanticSpecification.GivenClock");
        SetViews(entries, SemanticSurfaceDispositionKind.Ignored, "Not consumed by Axon emission; no atomic reaction execution guarantee is claimed. The reserved ATOMIC code has no runtime callsite.", "SemanticApplication.ReactionProductionMode SemanticReactionProductionMode.Atomic");
    }

    static void Warn(Dictionary<string, SemanticSurfaceDisposition> entries, string detail, string members)
    {
        foreach (var member in members.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var previous = entries[member];
            var prefix = previous.Kind == SemanticSurfaceDispositionKind.Warned ? previous.Detail + " " : string.Empty;
            entries[member] = new(SemanticSurfaceDispositionKind.Warned, prefix + detail + DegradationModes);
        }
    }
}
