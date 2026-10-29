// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Contracts.Rendering;
using static Cratis.Stage.Rendering.Axon.Admission.DegradationStub;

namespace Cratis.Stage.Rendering.Axon.Admission;

internal enum DegradationKind
{
    Unsupported,
    Informational
}

internal enum DegradationStub
{
    None,
    Comment,
    ThrowingBody,
    NoOpHandler,
    EmptyResult,
    Omitted,
    NoOpValidator,
    DenyAccess
}

internal sealed record Degradation(string Code, DegradationKind Kind, string Construct, string Reason,
    ArtifactRenderDiagnosticSeverity Severity, string? AxonDocs, DegradationStub Stub);

/// <summary>Documentation of renderer degradations; diagnostic severity is still decided at the call site.</summary>
internal static class Degradations
{
    const string Docs = "https://docs.axoniq.io/axon-framework-reference/5.4";
    static readonly Dictionary<string, Degradation> _byCode;

    static Degradations() => _byCode = All.ToDictionary(row => row.Code, StringComparer.Ordinal);

    internal static IReadOnlyList<Degradation> All { get; } = Array.AsReadOnly(
    [
        Informational(AxonRefusalCodes.Version, "render the modeled language and semantic version", "Only measured language and semantic pairs are admitted."),
        Informational(AxonRefusalCodes.Profile, "use the selected render profile", "The profile and mode inputs must be supported by this target."),
        Informational(AxonRefusalCodes.Scope, "render the selected model scopes", "Scopes must identify existing model elements."),
        Informational(AxonRefusalCodes.GapMode, "use the selected implementation-gap policy", "The implementation-gap policy must be known."),
        Informational(AxonRefusalCodes.RefusalMode, "use the selected refusal policy", "The refusal policy must be known."),
        Informational(AxonRefusalCodes.Scaffold, "generate an Axon application scaffold", "Scaffold admission is checked before emission."),
        Informational(AxonRefusalCodes.LocalRuntime, "use the renderer's matching runtime", "Source builds pin an unpublished runtime version."),
        Unsupported(AxonRefusalCodes.Names, "use the modeled Java names", "Reserved Java names must be renamed."),
        Unsupported(AxonRefusalCodes.SupportPackage, "use the modeled package name", "The package collides with a reserved support package."),
        Unsupported(AxonRefusalCodes.Generation, "generate the modeled values and artifacts", "The requested generation cannot be emitted as modeled; fallback artifacts may be used.", ThrowingBody),
        Unsupported(AxonRefusalCodes.ProjectionMapping, "project into the modeled read model", "The projection target or value mapping cannot be represented.", Omitted),
        Unsupported(AxonRefusalCodes.UnwrittenReadModel, "populate the modeled read model", "No admitted writer populates this read model.", EmptyResult),
        Unsupported(AxonRefusalCodes.ScopedProjection, "project scoped read-model state", "The projection scope cannot be resolved against the read model (a nested or child property that is missing or not a composite type, or a key that cannot be rendered), or a join re-applies event-context metadata to later children; the construct is skipped.", EmptyResult),
        Unsupported(AxonRefusalCodes.CompositeKey, "use the modeled composite projection key", "The projection key does not match the read model's key shape (a composite model needs every key part from the event).", Omitted),
        Unsupported(AxonRefusalCodes.KeyParts, "bind the modeled query key parts", "The query key parts cannot be bound to the read model.", ThrowingBody),
        Unsupported(AxonRefusalCodes.QueryFilter, "apply the modeled query filter", "The filter cannot be translated to the generated query.", ThrowingBody),
        Unsupported(AxonRefusalCodes.Reduction, "reduce events into the modeled state", "The reduction requires an admitted transition implementation and target.", NoOpHandler),
        Unsupported(AxonRefusalCodes.ObservableQuery, "execute the modeled query", "The query performer or argument shape is not supported.", ThrowingBody) with { AxonDocs = Docs + "/queries/query-dispatchers/" },
        Unsupported(AxonRefusalCodes.QueryAuthorization, "authorize the modeled query", "The modeled authorization cannot be enforced by this query handler.", ThrowingBody),
        Unsupported(AxonRefusalCodes.ViewSpecification, "verify the modeled view behavior", "The view specification cannot be executed by the generated fixture.", Comment),
        Unsupported(AxonRefusalCodes.TranslationCapture, "capture the modeled source data", "No source subscription or checkpoint is generated.", Omitted),
        Unsupported(AxonRefusalCodes.RefusalBranch, "execute the modeled refusal branch", "Command refusal branches are not represented by this reaction.", Omitted),
        Unsupported(AxonRefusalCodes.ConceptCompliance, "protect the modeled concept at rest", "Protection at rest is not configured.", ThrowingBody),
        Unsupported(AxonRefusalCodes.Schedule, "invoke commands on the modeled schedule", "Process-local scheduling cannot provide durable deadline semantics.", Comment) with { AxonDocs = Docs + "/migration/paths/deadlines/" },
        Unsupported(AxonRefusalCodes.Constraint, "enforce the modeled uniqueness constraint", "The constraint has no admitted atomic runtime shape.", ThrowingBody),
        Unsupported(AxonRefusalCodes.CommandMapping, "produce the modeled command result and events", "The command mapping cannot be represented.", ThrowingBody),
        Unsupported(AxonRefusalCodes.CommandSpecification, "verify the modeled command behavior", "The command fixture cannot establish this expectation.", Comment),
        Unsupported(AxonRefusalCodes.CommandValidation, "enforce the modeled command validation", "The validation has no admitted executable implementation.", ThrowingBody),
        Unsupported(AxonRefusalCodes.Clock, "read the modeled clock", "No durable clock implementation is supplied.", ThrowingBody) with { AxonDocs = Docs + "/migration/paths/deadlines/" },
        Unsupported(AxonRefusalCodes.Append, "append events with the modeled metadata", "The append metadata cannot be represented.", ThrowingBody),
        Unsupported(AxonRefusalCodes.Redelivery, "apply the modeled redelivery policy", "The modeled redelivery semantics are not implemented.", ThrowingBody),
        Unsupported(AxonRefusalCodes.Atomic, "execute the modeled behavior atomically", "The requested atomic boundary cannot be guaranteed.", ThrowingBody),
        Unsupported(AxonRefusalCodes.ProtectedReads, "protect the modeled decision reads", "The read cannot be enrolled in the required consistency boundary.", ThrowingBody),
        Unsupported(AxonRefusalCodes.Routing, "route the modeled command and events", "The routing shape cannot be represented.", ThrowingBody),
        Unsupported(AxonRefusalCodes.Implementation, "execute the modeled implementation", "An admitted implementation is required.", ThrowingBody),
        Unsupported(AxonRefusalCodes.ImplementationLanguage, "place the modeled implementation body", "Only admitted Java implementation bodies can be placed.", ThrowingBody),
        Unsupported(AxonRefusalCodes.Evolution, "read historical event revisions", "Historical revisions require an Axon upcaster implementation.", Comment),
        Unsupported(AxonRefusalCodes.Client, "represent the modeled client contract", "The contract shape cannot be typed by the generated TypeScript client.", Omitted),
        Informational(AxonRefusalCodes.ClientPrecision, "preserve modeled numeric precision in the client", "JSON numbers cannot carry every modeled value exactly."),
        Unsupported("STAGE-AXON-GENERATION-OPTIONS", "generate values using the declared options", "UUID value creation does not represent the declared generation options.", Comment),
        Unsupported("STAGE-AXON-TYPE", "represent the modeled property type", "The property type is not known to this target.", Comment),
        Unsupported("STAGE-AXON-IMPL-VALIDATION", "validate the modeled value", "The validation rule has no Java implementation; the generated validator accepts every value.", NoOpValidator),
        Unsupported("STAGE-AXON-0504", "execute the modeled reaction", "The reaction requires an admitted stateless handler or implementation.", NoOpHandler) with { AxonDocs = Docs + "/migration/paths/sagas/" },
        Unsupported("STAGE-AXON-0505", "invoke commands from the modeled trigger", "The trigger cannot be represented by the generated handler.", Omitted),
        Unsupported("STAGE-AXON-SPEC-CLAIMS", "preserve all modeled caller claims", "The security fixture cannot represent every modeled caller claim.", Comment),
        Unsupported("STAGE-AXON-EVENT-TAGS", "tag events with every modeled event source", "Axon events use one primary event-source tag.", Comment),
        Unsupported("STAGE-AXON-AUTHORIZATION", "enforce the modeled authorization policy", "Native metadata authority matching cannot express authenticated-only checks, negation, distinct-role conjunctions, or unsupported claims/custom-auth conditions; those handlers deny access.", DenyAccess),
        Unsupported("STAGE-AXON-JWT-PROVIDER", "validate tokens for the configured audience", "Credentials are rejected without a native JWT provider and explicit audience configuration.", DenyAccess),
        Unsupported("STAGE-AXON-HTTP-SPEC", "verify the modeled HTTP behavior", "The HTTP fixture cannot establish this expectation.", Comment),
        Unsupported("STAGE-AXON-SPEC", "verify the modeled specification", "No specification renderer establishes this expectation.", Comment)
    ]);

    internal static Degradation? Find(string code) => code is not null && _byCode.TryGetValue(code, out var row) ? row : null;

    static Degradation Unsupported(string code, string construct, string reason, DegradationStub stub = None) =>
        new(code, DegradationKind.Unsupported, construct, reason, ArtifactRenderDiagnosticSeverity.Warning, null, stub);

    static Degradation Informational(string code, string construct, string reason) =>
        new(code, DegradationKind.Informational, construct, reason, ArtifactRenderDiagnosticSeverity.Warning, null, None);
}
