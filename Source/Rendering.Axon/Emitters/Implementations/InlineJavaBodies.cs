// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using Cratis.Screenplay.Semantics;

namespace Cratis.Stage.Rendering.Axon.Emitters.Implementations;

internal sealed record InlineBodySlot(string? RequirementId, SemanticImplementationRole Role, SemanticId Owner);

internal static class InlineJavaBodies
{
    internal static bool TryResolve(AxonEmissionContext context, InlineBodySlot slot, out string body, out string reason)
    {
        var requirementId = slot.RequirementId;
        body = string.Empty;
        var candidates = Requirements(context).Where(requirement => requirement.RequirementId == requirementId).ToArray();
        reason = "The implementation requirement is unavailable or ambiguous.";
        if (string.IsNullOrEmpty(requirementId) || candidates.Length != 1) return false;
        var requirement = candidates[0];
        reason = ContractReason(requirement, slot.Role, slot.Owner);
        if (reason.Length > 0) return false;
        if (!context.Request.ImplementationContents.TryGetValue(requirementId, out var content) || string.IsNullOrWhiteSpace(content))
        {
            reason = "The resolved implementation content is unavailable.";
            return false;
        }
        if (content.Contains('\r'))
        {
            reason = "Native Java bodies must be LF-only: CR and CRLF cannot be transported verbatim by text artifacts.";
            return false;
        }
        if (Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content))) != requirement.ContentHash)
        {
            reason = "The implementation content does not match its recorded SHA-256 revision.";
            return false;
        }
        body = content;
        return true;
    }

    internal static IEnumerable<SemanticImplementationRequirement> Requirements(AxonEmissionContext context) =>
        context.Request.ImplementationRequirements.IsDefault ? [] : context.Request.ImplementationRequirements;

    static SemanticKind OwnerKind(SemanticImplementationRole role) => role switch
    {
        SemanticImplementationRole.CommandHandler => SemanticKind.Command,
        SemanticImplementationRole.ReactionEffect => SemanticKind.Reaction,
        SemanticImplementationRole.QueryPerformer => SemanticKind.Query,
        _ => SemanticKind.Unknown
    };

    static string ContractReason(SemanticImplementationRequirement requirement, SemanticImplementationRole role, SemanticId owner)
    {
        (bool Invalid, string Reason)[] checks =
        [
            (requirement.Role != role || requirement.Source.SemanticId != owner, "The implementation role or semantic owner does not match the slot."),
            (requirement.Owner.Kind != OwnerKind(role), "The implementation address does not identify the slot's owner kind."),
            (requirement.Language != "java", "Only the registered java language has a native Axon body slot."),
            (requirement.File is not null, "File-backed implementations have no supported native body slot."),
            (requirement.ContextVersion != 1 || requirement.ResultVersion != 1, "The implementation context or result contract revision is unsupported."),
            (requirement.IsAmbiguous || requirement.IsProvisional, "Ambiguous or provisional implementation identities cannot be placed."),
            (requirement.AttachmentResolution != SemanticAttachmentResolution.Resolved, "The implementation attachment has not been resolved."),
            (requirement.RequiredCapability != "provider-defined", "The implementation capability is not supported by this native body slot.")
        ];
        return checks.FirstOrDefault(check => check.Invalid).Reason ?? string.Empty;
    }
}
