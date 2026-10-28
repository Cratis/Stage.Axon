// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Sdk;

internal static partial class SemanticSurfaceLedger
{
    static void ClassifyImplementations(LedgerBuilder rows)
    {
        const string nativeMembers = """
            SemanticCommandHandler.RequirementId SemanticCommandHandler.ResultVersion
            SemanticImplementationRole.CommandHandler SemanticImplementationRole.ReactionEffect
            SemanticImplementationRequirement.RequirementId SemanticImplementationRequirement.Role SemanticImplementationRequirement.Owner
            SemanticImplementationRequirement.Source SemanticImplementationRequirement.Language SemanticImplementationRequirement.File
            SemanticImplementationRequirement.ContentHash SemanticImplementationRequirement.ContextVersion SemanticImplementationRequirement.ResultVersion
            SemanticImplementationRequirement.RequiredCapability SemanticImplementationRequirement.IsAmbiguous SemanticImplementationRequirement.IsProvisional
            SemanticImplementationRequirement.AttachmentResolution
            """;
        const string native = "Conditional java method slots admit LF-only, SHA-256-matched inline command/reaction/query bodies with supported version-1 contracts. Selected unconsumed null/java requirements report STAGE-AXON-IMPL; other languages report STAGE-AXON-IMPL-LANGUAGE. Command result revision or slot resolution failure emits a throwing handler; this does not admit reducer bodies." + DegradationModes;
        rows.Warned(["STAGE-AXON-IMPL", "STAGE-AXON-IMPL-LANGUAGE"], native, nativeMembers);
        rows.Warned(["STAGE-AXON-IMPL", "STAGE-AXON-IMPL-LANGUAGE", "STAGE-AXON-0801"], native + " " + ConstraintsDetail, "SemanticCommand.Handler");
        rows.Warned(["STAGE-AXON-IMPL", "STAGE-AXON-IMPL-LANGUAGE", "STAGE-AXON-0504", "STAGE-AXON-0506"], native + " " + ReactionDetail, "SemanticReactionTrigger.RequirementId");
        rows.Warned(["STAGE-AXON-IMPL", "STAGE-AXON-IMPL-LANGUAGE", "STAGE-AXON-0401"], native + " " + QueryDetail, "SemanticImplementationRole.QueryPerformer SemanticImplementationRequirement.QueryShape");
        rows.Warned(["STAGE-AXON-0306"], "Conditional pure Java reducer transitions use the same LF-only, SHA-256-matched version-1 admission rules. Unavailable transitions retain STAGE-AXON-0306 and implementation diagnostics and quarantine their entire read model: no writer projector is registered, all store operations refuse, and dependent modeled queries refuse. ALL opaque Java query performers refuse while any model is quarantined because their dependencies are not modeled, including unrelated performers. Dependent view specifications execute enabled store-refusal proofs rather than asserting successful reduction.", "SemanticImplementationRole.ReducerTransition");
        rows.Ignored(IgnoredReason.Identity, "Member is a descriptive slot label; binding identifies the executable slot by requirement id, role and semantic owner rather than by this redundant member name.", "SemanticImplementationRequirement.Member");
        rows.Warned(["STAGE-AXON-IMPL", "STAGE-AXON-IMPL-LANGUAGE"], native + " BodyLines are not the executable body: placement uses resolved implementation sidecar content and verifies its SHA-256 hash. Missing content is reported rather than falling back to these source lines.", "SemanticImplementationRequirement.BodyLines");
        rows.Warned(["STAGE-AXON-IMPL", "STAGE-AXON-IMPL-LANGUAGE"], native + " Only Resolved attachments can be placed; Pending and UnresolvedFile fail the native slot contract and report the owning implementation requirement.", "SemanticAttachmentResolution.Resolved SemanticAttachmentResolution.Pending SemanticAttachmentResolution.UnresolvedFile");
        rows.Warned(["STAGE-AXON-IMPL", "STAGE-AXON-IMPL-LANGUAGE"], native + " Capability enum symbols are not read as execution switches. Admission uses the owning requirement's RequiredCapability string and role, and unsupported or unplaced requirements are reported; these symbols do not independently grant native execution.", "SemanticExecutionCapability.ImplementationBody SemanticExecutionCapability.Command SemanticExecutionCapability.Query SemanticExecutionCapability.IdentityAllocation SemanticExecutionCapability.Projection SemanticExecutionCapability.Specification SemanticExecutionCapability.Authorization SemanticExecutionCapability.Reaction");
        rows.Ignored(IgnoredReason.UnknownSentinel, "Unknown capability has no independently executable interpretation.", "SemanticExecutionCapability.Unknown");
        rows.Warned(["STAGE-AXON-IMPL", "STAGE-AXON-0107"], native + " " + ValidationDetail + " Command-validation bodies have no native method slot; behavior admission reports their implementation requirements and command emission reports the unsupported validator.", "SemanticImplementationRole.CommandValidation");
    }
}
