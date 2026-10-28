// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using Cratis.Screenplay.Semantics;

namespace Cratis.Stage.Rendering.Axon.Emitters.Implementations;

/// <summary>
/// Identifies a body slot. <paramref name="Slice"/> and <paramref name="MemberPrefix"/> additionally accept the shape the Screenplay compiler
/// produces for reaction triggers: a slice-owned requirement whose member begins with the owning reaction's name.
/// </summary>
internal sealed record InlineBodySlot(string? RequirementId, SemanticImplementationRole Role, SemanticId Owner, SemanticId? Slice = null, string? MemberPrefix = null)
{
    /// <summary>Creates the one slot a reaction trigger body is placed into and audited against: reaction-owned, or slice-owned with the reaction's name as member prefix.</summary>
    /// <param name="requirementId">The trigger's implementation requirement identifier.</param>
    /// <param name="reaction">The reaction owning the trigger.</param>
    /// <param name="slice">The identifier of the slice containing the reaction.</param>
    /// <returns>The reaction slot.</returns>
    internal static InlineBodySlot ForReaction(string? requirementId, SemanticReaction reaction, SemanticId? slice) =>
        new(requirementId, SemanticImplementationRole.ReactionEffect, reaction.Id, slice, reaction.Name + "/");

    /// <summary>Gets whether the requirement addresses this slot, either directly owned or slice-owned with the member prefix.</summary>
    /// <param name="requirement">The requirement to test.</param>
    /// <returns><see langword="true"/> when the requirement belongs to the slot.</returns>
    internal bool Addresses(SemanticImplementationRequirement requirement) =>
        requirement.Role == Role && (requirement.Source.SemanticId == Owner || InlineJavaBodies.SliceOwned(requirement, this));
}

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
        var sliceOwned = SliceOwned(requirement, slot);
        reason = ContractReason(requirement, slot.Role, sliceOwned ? requirement.Source.SemanticId : slot.Owner, sliceOwned ? SemanticKind.Slice : OwnerKind(slot.Role));
        if (reason.Length > 0) return false;
        if (!context.Request.ImplementationContents.TryGetValue(requirementId, out var content) || string.IsNullOrWhiteSpace(content))
        {
            reason = "The resolved implementation content is unavailable.";
            return false;
        }
        reason = ContentReason(content, requirement);
        if (reason.Length > 0) return false;
        body = content;
        return true;
    }

    internal static IEnumerable<SemanticImplementationRequirement> Requirements(AxonEmissionContext context) =>
        context.Request.ImplementationRequirements.IsDefault ? [] : context.Request.ImplementationRequirements;

    internal static bool SliceOwned(SemanticImplementationRequirement requirement, InlineBodySlot slot) =>
        slot.Slice is { } slice && slot.MemberPrefix is { } prefix && requirement.Role == slot.Role && requirement.Source.SemanticId == slice &&
        requirement.Owner.Kind == SemanticKind.Slice && requirement.Member?.StartsWith(prefix, StringComparison.Ordinal) == true;

    static string ContentReason(string content, SemanticImplementationRequirement requirement)
    {
        (bool Invalid, string Reason)[] checks =
        [
            (content.Contains('\r'), "Native Java bodies must be LF-only: CR and CRLF cannot be transported verbatim by text artifacts."),
            (Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content))) != requirement.ContentHash, "The implementation content does not match its recorded SHA-256 revision."),
            (requirement.Role == SemanticImplementationRole.PolicyPredicate && content.Contains("\\u", StringComparison.Ordinal), "Java Unicode escapes are not supported in inline policy bodies; access is denied.")
        ];
        return checks.FirstOrDefault(check => check.Invalid).Reason ?? string.Empty;
    }

    static SemanticKind OwnerKind(SemanticImplementationRole role) => role switch
    {
        SemanticImplementationRole.CommandHandler => SemanticKind.Command,
        SemanticImplementationRole.ReactionEffect => SemanticKind.Reaction,
        SemanticImplementationRole.QueryPerformer => SemanticKind.Query,
        SemanticImplementationRole.ReducerTransition => SemanticKind.Slice,
        SemanticImplementationRole.PolicyPredicate => SemanticKind.Application,
        _ => SemanticKind.Unknown
    };

    static string ForeignLanguage(string language) =>
        $"The Axon target only realizes Java bodies; language '{language}' is not supported. Supply the body in Java (or render it with a target that realizes this language) and render again.";

    static string ContractReason(SemanticImplementationRequirement requirement, SemanticImplementationRole role, SemanticId owner, SemanticKind ownerKind)
    {
        (bool Invalid, string Reason)[] checks =
        [
            (requirement.Role != role || requirement.Source.SemanticId != owner, "The implementation role or semantic owner does not match the slot."),
            (requirement.Owner.Kind != ownerKind, "The implementation address does not identify the slot's owner kind."),
            (requirement.Language is null, "The implementation language is unknown. Supply the body in Java and render again."),
            (requirement.Language != "java", ForeignLanguage(requirement.Language!)),
            (requirement.File is not null, "File-backed implementations have no supported native body slot."),
            (requirement.ContextVersion != 1 || requirement.ResultVersion != 1, "The implementation context or result contract revision is unsupported."),
            (requirement.IsAmbiguous || requirement.IsProvisional, "Ambiguous or provisional implementation identities cannot be placed."),
            (requirement.AttachmentResolution != SemanticAttachmentResolution.Resolved, "The implementation attachment has not been resolved."),
            (requirement.RequiredCapability != (role is SemanticImplementationRole.ReducerTransition or SemanticImplementationRole.PolicyPredicate ? "pure" : "provider-defined"), "The implementation capability is not supported by this native body slot.")
        ];
        return checks.FirstOrDefault(check => check.Invalid).Reason ?? string.Empty;
    }
}
