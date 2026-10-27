// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Sdk;

internal static partial class SemanticSurfaceLedger
{
    internal const string Unclassified = "UNCLASSIFIED";
    internal const string UnreadBehavioral = "UNREAD-BEHAVIORAL: no Axon emitter reads this member and it carries no code of its own; effects are reported at the owning member (verified per member against the emitters' unsupported branches), not here.";

    const string MetadataTypes = """
        Diagnostic DiagnosticSeverity SemanticSourceMapEntry SemanticSourceMapRole SemanticSourceSpan SemanticAddressPartKind SemanticIdentityOrigin
        SemanticImplementationBodySpan DocumentId ApplicationIdentity SemanticRevision SemanticId EventContractId EventContractRevision SemanticKind
        """;

    const string MetadataMembers = """
        ExecutableSemanticModel.DeprecationDiagnostics SemanticEventContract.Origin SemanticImplementationRequirement.BodySpan SemanticImplementationRequirement.Hints
        """;

    const string UnreadMembers = """
        SemanticGeneration.Case SemanticGeneration.Size SemanticGeneration.Alphabet SemanticGeneration.Pattern SemanticGeneration.Checksum
        SemanticValidationRuleKind.Checksum SemanticValidationRuleKind.RulePredicate SemanticValidationRuleKind.CodeValidation
        SemanticValidationRuleKind.DecimalPlaces SemanticValidationSeverity.Information SemanticImplementationRequirement.BodyLines
        SemanticAttachmentResolution.UnresolvedFile SemanticAttachmentResolution.Pending SemanticExecutionCapability.ImplementationBody
        SemanticExecutionCapability.IdentityAllocation SemanticExpressionKind.ReadPath SemanticEventContextValueKind.CausedByName
        SemanticEventContextValueKind.CausedByUserName SemanticValueKind.Null SemanticClaimTargetKind.Subject SemanticReadPathExpression.PropertyId
        SemanticPerformedQueryArgumentKind.Filter
        """;

    const string CaptureTypes = """
        SemanticSpecificationCapture SemanticTranslationDirection
        """;

    const string CaptureMembers = """
        SemanticSlice.Captures SemanticSlice.Direction SemanticSliceKind.Translate SemanticEventContract.Visibility SemanticEventContract.ContractId SemanticEventVisibility.Public
        SemanticSpecification.GivenCaptures SemanticSpecification.WhenCapture
        """;

    const string RoutingMembers = """
        SemanticEventSource.SourceKind SemanticEventStream.StreamIdParts SemanticEventStream.StreamIdType SemanticEventStream.StreamKind
        """;

    const string FixtureRouteMembers = """
        SemanticFixtureRoute.StreamId SemanticFixtureRoute.StreamIdParts SemanticFixtureRoutePart.Part
        """;

    const string UnplacedRoleMembers = """
        SemanticImplementationRole.ConceptValidation SemanticImplementationRole.RulePredicate SemanticImplementationRole.PolicyPredicate
        SemanticImplementationRole.ConstraintPredicate SemanticImplementationRole.ValueGeneration
        """;

    const string RefusalMembers = """
        SemanticInvocation.OnRefused
        """;

    const string RefusalTypes = """
        SemanticInvocationRefusal SemanticRefusalExpression
        """;

    static void ClassifyBehaviors(Dictionary<string, SemanticSurfaceDisposition> entries)
    {
        var unread = new HashSet<string>(UnreadMembers.Split([' ', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.Ordinal);
        var metadataTypes = new HashSet<string>(MetadataTypes.Split([' ', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.Ordinal);
        var metadataMembers = new HashSet<string>(MetadataMembers.Split([' ', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.Ordinal);
        foreach (var key in entries.Where(entry => entry.Value.Detail == Unclassified).Select(entry => entry.Key).ToArray())
        {
            var type = key[..key.IndexOf('.', StringComparison.Ordinal)];
            entries[key] = key switch
            {
                _ when metadataTypes.Contains(type) || metadataMembers.Contains(key) || key.EndsWith(".IsSet", StringComparison.Ordinal) || key.EndsWith(".$type", StringComparison.Ordinal)
                    => new(SemanticSurfaceDispositionKind.Ignored, "Identity, provenance or diagnostic metadata; no runtime behavior."),
                _ when unread.Contains(key) => new(SemanticSurfaceDispositionKind.Ignored, UnreadBehavioral),
                _ => new(SemanticSurfaceDispositionKind.Rendered, "Read by the Axon emitters under " + EmitterFolder(type) + "; unsupported forms degrade at their owner.")
            };
        }

        Warn(entries, "STAGE-AXON-0601: captures, translations and public event contracts are not rendered. No source subscription, checkpoint, translation or publication is generated; the central behavior admission reports one 0601 per capture, per translate slice without captures and per public event contract, and writes a comment in the owning package-info.java. The event itself is still rendered as a private Axon event.", string.Join(' ', entries.Keys.Where(key => key.StartsWith("SemanticCapture", StringComparison.Ordinal))) + ' ' + MembersOf(entries, CaptureTypes, CaptureMembers));
        Warn(entries, "STAGE-AXON-ROUTING: a source kind, stream kind or stream identity shape is not represented; only the primary event-source tag is used. CommandHandlerEmitter reports it for appending commands; the central behavior admission reports it for native handlers and commands that append nothing.", Split(RoutingMembers));
        Warn(entries, "STAGE-AXON-0112: explicit fixture event routes make the generated specification unsupported; the specification is reported and disabled at its owner.", Split(FixtureRouteMembers));
        Warn(entries, "STAGE-AXON-IMPL: no inline body slot places this implementation role; the central behavior admission reports each unconsumed requirement at its source.", Split(UnplacedRoleMembers));
        Warn(entries, "STAGE-AXON-0506: an invocation that declares refusal branches (OnRefused) reports one 0506 per branch at the owning reaction. The branches are not rendered, and the reaction is also not registered (0504).", MembersOf(entries, RefusalTypes, RefusalMembers));
    }

    static string Split(string members) => string.Join(' ', members.Split([' ', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    static string MembersOf(Dictionary<string, SemanticSurfaceDisposition> entries, string types, string members)
    {
        var typeSet = new HashSet<string>(types.Split([' ', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.Ordinal);
        var memberSet = members.Split([' ', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', entries.Keys.Where(key => typeSet.Contains(key[..key.IndexOf('.', StringComparison.Ordinal)])).Concat(memberSet));
    }

    static string EmitterFolder(string type) => type switch
    {
        _ when type.StartsWith("SemanticCommand", StringComparison.Ordinal) || type.StartsWith("SemanticDecision", StringComparison.Ordinal) || type.StartsWith("SemanticProduced", StringComparison.Ordinal) => "Emitters/Commands",
        _ when type.StartsWith("SemanticEvent", StringComparison.Ordinal) => "Emitters/Events",
        _ when type.StartsWith("SemanticReaction", StringComparison.Ordinal) || type.StartsWith("SemanticInvocation", StringComparison.Ordinal) => "Emitters/Automations",
        _ when type.StartsWith("SemanticQuery", StringComparison.Ordinal) || type.StartsWith("SemanticKeyedQuery", StringComparison.Ordinal) => "Emitters/Queries",
        _ when type.StartsWith("SemanticProjection", StringComparison.Ordinal) || type.StartsWith("SemanticReducer", StringComparison.Ordinal) || type.StartsWith("SemanticReadModel", StringComparison.Ordinal) => "Emitters/Projections",
        _ when type.StartsWith("SemanticConcept", StringComparison.Ordinal) || type.StartsWith("SemanticValidation", StringComparison.Ordinal) => "Emitters/Concepts",
        _ when type.StartsWith("SemanticSpecification", StringComparison.Ordinal) => "Emitters/Specifications",
        _ when type.StartsWith("SemanticImplementation", StringComparison.Ordinal) => "Emitters/Implementations",
        _ => "Emitters",
    };
}
