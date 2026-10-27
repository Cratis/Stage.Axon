// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Sdk;

internal static partial class SemanticSurfaceLedger
{
    const string ChangesMembers = """
        SemanticCommand Id Name Properties Validations Produces Destination Requirements Authorization Route Handler CodeValidations Response Reads
        SemanticCommandHandler RequirementId ResultVersion
        SemanticCodeValidation RequirementId
        SemanticCommandRoute Source Stream StreamId StreamIdParts
        SemanticCommandRoutePart Part Value
        """;

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
        rows.Warned(["STAGE-AXON-0306"], "Conditional pure Java reducer transitions use the same LF-only, SHA-256-matched version-1 admission rules. Unplaced transitions retain STAGE-AXON-0306 and implementation diagnostics; dependent specifications remain disabled unless fully supported.", "SemanticImplementationRole.ReducerTransition");
    }
}
