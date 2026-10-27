// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Sdk;

internal static partial class SemanticSurfaceLedger
{
    static void ClassifyModel(LedgerBuilder rows)
    {
        rows.Rendered("Application selection and scope traversal determine the emitted declarations and artifact paths.", "ExecutableSemanticModel.Application SemanticApplication.Modules SemanticApplication.Id SemanticModule.Features SemanticModule.Id SemanticModule.Name SemanticFeature.Features SemanticFeature.Id SemanticFeature.Name SemanticFeature.Slices SemanticSlice.Id SemanticSlice.Name", typeof(ScopeSelection));
        rows.Rendered("The application name supplies the default generated Java base package when no explicit package is configured.", "SemanticApplication.Name", typeof(AxonNamesInput));
        rows.Rendered("Concept and composite declarations resolve generated command Java types.", "SemanticApplication.Concepts SemanticApplication.Types", typeof(Emitters.Commands.CommandJavaTypes));
        rows.Rendered("Application event sources resolve the primary route's tag and identifier type.", "SemanticApplication.EventSources", typeof(Emitters.Events.AxonEventSources));
        rows.Rendered("Declared application triggers produce trigger endpoints.", "SemanticApplication.Triggers", typeof(Emitters.Automations.ApplicationTriggersEmitter));
        rows.Rendered("Selected command declarations produce command contracts and handlers.", "SemanticSlice.Commands", typeof(Emitters.Commands.AxonCommandsEmitter));
        rows.Rendered("Selected event declarations produce event records.", "SemanticSlice.Events", typeof(Emitters.Events.AxonEventsEmitter));
        rows.Rendered("Read models, projections and reducers select their generated view artifacts.", "SemanticSlice.ReadModels SemanticSlice.Projections SemanticSlice.Reducers", typeof(Emitters.Projections.AxonProjectionsEmitter));
        rows.Rendered("Selected query declarations produce query contracts and handlers.", "SemanticSlice.Queries", typeof(Emitters.Queries.AxonQueriesEmitter));
        rows.Rendered("Selected reactions produce automation artifacts.", "SemanticSlice.Reactions", typeof(Emitters.Automations.AxonAutomationsEmitter));
        rows.Warned(["STAGE-AXON-VERSION"], VersionDetail + " Major and Minor are inspected by version admission, not consumed as emitter behavior.", "LanguageVersion.Major LanguageVersion.Minor SemanticVersion.Major SemanticVersion.Minor");
        rows.Warned(["STAGE-AXON-0601"], CaptureDetail + " Slice.Kind is inspected by behavior admission for translation; other category labels do not select emission of the contained declarations.", "SemanticSlice.Kind");
        rows.Warned(["STAGE-AXON-0504"], "STAGE-AXON-0504: ReactionProductionMode and its sole Atomic value are not read by the emitters. Registration requires a single supported effect; multi-effect triggers are refused, rather than implementing a separately selected atomic execution mode." + DegradationModes, "SemanticApplication.ReactionProductionMode SemanticReactionProductionMode.Atomic");
        rows.Ignored(IgnoredReason.Identity, "Model revision and semantic addresses describe declaration identity; they do not select runtime behavior.", "ExecutableSemanticModel.Revision SemanticAddress.Kind SemanticAddress.Parts SemanticAddress.Name SemanticAddress.Application SemanticAddress.OwnerKind SemanticAddressPart.Kind SemanticAddressPart.Key");
        rows.Ignored(IgnoredReason.Identity, "Slice category labels identify declarations; contained commands, views and reactions select emitted artifacts independently of these labels.", "SemanticSliceKind.StateChange SemanticSliceKind.StateView SemanticSliceKind.Automation");
        rows.Ignored(IgnoredReason.UnknownSentinel, "Unknown slice category has no additional executable interpretation.", "SemanticSliceKind.Unknown");
        rows.Ignored(IgnoredReason.CompilerDiagnostics, "Diagnostic locations and argument payloads are compiler reporting metadata, not runtime behavior.", "SourceLocation.Line SourceLocation.Column SourceLocation.Path DiagnosticArguments.Count DiagnosticArguments.Keys DiagnosticArguments.Values DiagnosticArgument.Kind DiagnosticArgument.Value CodeBlockSourceLine.Line CodeBlockSourceLine.Column");
    }

    static void ClassifyMetadata(LedgerBuilder rows)
    {
        const string metadata = "Identity, provenance or diagnostic metadata; no runtime behavior.";
        rows.Ignored(IgnoredReason.Identity, metadata, """
            SemanticRevision.IsSet SemanticId.IsSet EventContractId.IsSet EventContractRevision.IsValid ApplicationIdentity.IsSet DocumentId.IsSet
            SemanticKind.Application SemanticKind.Module SemanticKind.Feature SemanticKind.Slice SemanticKind.Concept SemanticKind.CompositeType SemanticKind.Property SemanticKind.Command
            SemanticKind.EventContract SemanticKind.ReadModel SemanticKind.Projection SemanticKind.Query SemanticKind.Specification SemanticKind.QueryArgument SemanticKind.Trigger SemanticKind.Reaction
            SemanticKind.Capture SemanticKind.EventSource SemanticKind.EventStream SemanticKind.Unknown
            SemanticAddressPartKind.Application SemanticAddressPartKind.Module SemanticAddressPartKind.Feature SemanticAddressPartKind.Slice SemanticAddressPartKind.Declaration
            SemanticAddressPartKind.Member SemanticAddressPartKind.Discriminator SemanticAddressPartKind.OwnerKind SemanticAddressPartKind.Generation SemanticAddressPartKind.Unknown
            SemanticAuthorization.$type SemanticCondition.$type
            SemanticPolicyCondition.$type SemanticSpecificationResponse.$type
            SemanticIdentityOrigin.Persisted SemanticIdentityOrigin.LegacyBootstrap SemanticIdentityOrigin.Unknown
            """);
        rows.Ignored(IgnoredReason.SourceProvenance, metadata, """
            SemanticSourceMapEntry.SemanticId SemanticSourceMapEntry.Span SemanticSourceMapEntry.Origin SemanticSourceMapEntry.Role
            SemanticSourceSpan.Document SemanticSourceSpan.Start SemanticSourceSpan.Length SemanticSourceSpan.StartLine SemanticSourceSpan.StartColumn SemanticSourceSpan.EndLine SemanticSourceSpan.EndColumn
            SemanticSourceMapRole.Declaration SemanticSourceMapRole.Description SemanticSourceMapRole.Unknown SemanticEventContract.Origin
            SemanticImplementationRequirement.BodySpan SemanticImplementationRequirement.Hints
            SemanticImplementationBodySpan.Start SemanticImplementationBodySpan.End SemanticImplementationBodySpan.StartLine SemanticImplementationBodySpan.StartColumn
            SemanticImplementationBodySpan.EndLine SemanticImplementationBodySpan.EndColumn
            """);
        rows.Ignored(IgnoredReason.CompilerDiagnostics, metadata, """
            Diagnostic.Severity Diagnostic.Code Diagnostic.Message Diagnostic.Location Diagnostic.Arguments
            DiagnosticSeverity.Information DiagnosticSeverity.Warning DiagnosticSeverity.Error ExecutableSemanticModel.DeprecationDiagnostics
            """);
    }
}
