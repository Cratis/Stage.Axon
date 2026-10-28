// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Sdk;

internal static partial class SemanticSurfaceLedger
{
    const string CaptureDetail = "STAGE-AXON-0601: captures, translations and public event contracts are not rendered. No source subscription, checkpoint, translation or publication is generated; the central behavior admission reports one 0601 per capture, per translate slice without captures and per public event contract, and writes a comment in the owning package-info.java. The event itself is still rendered as a private Axon event." + DegradationModes;

    static void ClassifyBehaviors(LedgerBuilder rows)
    {
        rows.Rendered("Application trigger identity, name and properties select the registered trigger record and HTTP endpoint whose private effect methods send the commands.", "SemanticApplicationTrigger.Id SemanticApplicationTrigger.Name SemanticApplicationTrigger.Properties", typeof(Emitters.Automations.ApplicationTriggersEmitter));
        rows.Rendered("Reaction identity and name determine the automation artifact and its diagnostic attribution.", "SemanticReaction.Id SemanticReaction.Name", typeof(Emitters.Automations.AxonAutomationsEmitter));
        rows.Warned(["STAGE-AXON-0504", "STAGE-AXON-0506"], ReactionDetail + " Command resolution and argument mappings must prepare completely; unsupported mappings prevent reaction registration.", "SemanticInvocation.Command SemanticInvocation.Mappings");
        rows.Rendered("Event, application-trigger and lifecycle trigger kinds select native @EventHandler, lifecycle and endpoint methods; effect and guard admission is separate.", "SemanticReactionTriggerKind.Event SemanticReactionTriggerKind.ApplicationTrigger SemanticReactionTriggerKind.Startup SemanticReactionTriggerKind.Shutdown", typeof(Emitters.Automations.ReactionScheduling));
        rows.Warned(["STAGE-AXON-0504", "STAGE-AXON-0506"], ReactionDetail + " Unknown trigger kinds have no handler annotation and fail preparation.", "SemanticReactionTriggerKind.Unknown");
        rows.Rendered("System identity is explicitly admitted by trigger preparation; dispatch establishes the modeled roles.", "SemanticReactionIdentityKind.System", typeof(Emitters.Automations.ReactionTriggerEmitter));
        rows.Warned(["STAGE-AXON-0504", "STAGE-AXON-0506"], ReactionDetail + " Observer Source and Stream are not read independently: any nonnull reaction.From fails effect preparation and is reported at the reaction owner.", "SemanticObserverFilter.Source SemanticObserverFilter.Stream");
        rows.Warned(["STAGE-AXON-0601"], CaptureDetail, """
            SemanticSlice.Captures SemanticSlice.Direction SemanticSliceKind.Translate SemanticEventContract.ContractId SemanticEventContract.Visibility
            SemanticCapture.Appends SemanticCapture.Children SemanticCapture.Id SemanticCapture.Key SemanticCapture.Map SemanticCapture.Name SemanticCapture.Nested SemanticCapture.EventsSource
            SemanticCaptureAppend.EventContract SemanticCaptureAppend.EventSourceType SemanticCaptureAppend.Mappings SemanticCaptureAppend.Tags SemanticCaptureAppend.When
            SemanticCaptureChildren.Appends SemanticCaptureChildren.Field SemanticCaptureChildren.IdentifiedBy SemanticCaptureChildren.Map
            SemanticCaptureNested.Appends SemanticCaptureNested.Field SemanticCaptureNested.Map SemanticCaptureRecord.Fields
            SemanticCaptureField.Kind SemanticCaptureField.Name SemanticCaptureField.Record SemanticCaptureField.Records SemanticCaptureField.Value
            SemanticCaptureFieldKind.Unknown SemanticCaptureFieldKind.Record SemanticCaptureFieldKind.Records SemanticCaptureFieldKind.Value
            SemanticCaptureCondition.Expression SemanticCaptureCondition.Fields SemanticCaptureCondition.From SemanticCaptureCondition.Kind SemanticCaptureCondition.To
            SemanticCaptureConditionKind.Unknown SemanticCaptureConditionKind.Added SemanticCaptureConditionKind.AllChanged SemanticCaptureConditionKind.AnyChanged SemanticCaptureConditionKind.Expression SemanticCaptureConditionKind.Removed SemanticCaptureConditionKind.Transition
            SemanticCaptureMap.Kind SemanticCaptureMap.Separator SemanticCaptureMap.Source SemanticCaptureMap.Targets SemanticCaptureMap.Template SemanticCaptureMap.Translations
            SemanticCaptureMapKind.Unknown SemanticCaptureMapKind.Split SemanticCaptureMapKind.Template SemanticCaptureMapKind.Value
            SemanticCaptureMapping.Field SemanticCaptureMapping.TargetProperty SemanticCaptureMapping.Value SemanticCaptureTemplatePart.Field SemanticCaptureTemplatePart.Text
            SemanticCaptureTranslation.From SemanticCaptureTranslation.To SemanticSpecificationCapture.Capture SemanticSpecificationCapture.Record SemanticEventVisibility.Public
            SemanticCaptureEventsSource.Events SemanticCaptureEventsSource.KeyIsEventSource SemanticTranslationDirection.Inbound SemanticTranslationDirection.Outbound
            """);
        rows.Warned(["STAGE-AXON-ROUTING"], "STAGE-AXON-ROUTING: a source kind, stream kind or stream identity shape is not represented; only the primary event-source tag is used. CommandHandlerEmitter reports it for appending commands; the central behavior admission reports it for native handlers and commands that append nothing." + DegradationModes, "SemanticEventSource.SourceKind SemanticEventStream.StreamIdParts SemanticEventStream.StreamIdType SemanticEventStream.StreamKind");
        rows.Warned(["STAGE-AXON-0112"], "STAGE-AXON-0112: explicit fixture event routes make the generated specification unsupported; the specification is reported and disabled at its owner." + DegradationModes, "SemanticFixtureRoute.StreamId SemanticFixtureRoute.StreamIdParts SemanticFixtureRoutePart.Part");
        rows.Warned(["STAGE-AXON-IMPL"], "STAGE-AXON-IMPL: no inline body slot places this implementation role; the central behavior admission reports each unconsumed requirement at its source." + DegradationModes, "SemanticImplementationRole.ConceptValidation SemanticImplementationRole.RulePredicate SemanticImplementationRole.ConstraintPredicate SemanticImplementationRole.ValueGeneration");
        rows.Warned(["STAGE-AXON-0506"], "STAGE-AXON-0506: an invocation that declares refusal branches (OnRefused) reports one 0506 per branch at the owning reaction. The branches are not rendered, and the reaction is also not registered (0504)." + DegradationModes, "SemanticInvocation.OnRefused SemanticInvocationRefusal.Selector SemanticInvocationRefusal.Constraint SemanticInvocationRefusal.Produces SemanticRefusalExpression.Member SemanticRefusalExpression.Type");
    }
}
