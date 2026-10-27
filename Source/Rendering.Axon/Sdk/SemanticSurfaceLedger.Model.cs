// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Sdk;

internal static partial class SemanticSurfaceLedger
{
    static void ClassifyMetadata(LedgerBuilder rows)
    {
        const string metadata = "Identity, provenance or diagnostic metadata; no runtime behavior.";
        rows.Ignored(IgnoredReason.Identity, metadata, """
            SemanticRevision.IsSet SemanticId.IsSet EventContractId.IsSet EventContractRevision.IsValid EventContractRevision.Value ApplicationIdentity.IsSet DocumentId.IsSet
            SemanticKind.Application SemanticKind.Module SemanticKind.Feature SemanticKind.Slice SemanticKind.Concept SemanticKind.CompositeType SemanticKind.Property SemanticKind.Command
            SemanticKind.EventContract SemanticKind.ReadModel SemanticKind.Projection SemanticKind.Query SemanticKind.Specification SemanticKind.QueryArgument SemanticKind.Trigger SemanticKind.Reaction
            SemanticKind.Capture SemanticKind.EventSource SemanticKind.EventStream SemanticKind.Unknown
            SemanticAddressPartKind.Application SemanticAddressPartKind.Module SemanticAddressPartKind.Feature SemanticAddressPartKind.Slice SemanticAddressPartKind.Declaration
            SemanticAddressPartKind.Member SemanticAddressPartKind.Discriminator SemanticAddressPartKind.OwnerKind SemanticAddressPartKind.Generation SemanticAddressPartKind.Unknown
            SemanticNullValue.$type SemanticAuthenticatedCondition.$type SemanticAnonymousCondition.$type SemanticAuthorization.$type SemanticCondition.$type
            SemanticPolicyCondition.$type SemanticProjectionEventSourceIdentity.$type SemanticSpecificationResponse.$type
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
