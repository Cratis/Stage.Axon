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

    static void ClassifyImplementations(Dictionary<string, SemanticSurfaceDisposition> entries)
    {
        const string nativeMembers = """
            SemanticCommand.Handler SemanticCommandHandler.RequirementId SemanticCommandHandler.ResultVersion
            SemanticReactionTrigger.RequirementId
            SemanticImplementationRole.CommandHandler SemanticImplementationRole.ReactionEffect SemanticImplementationRole.QueryPerformer
            SemanticImplementationRequirement.RequirementId SemanticImplementationRequirement.Role SemanticImplementationRequirement.Owner
            SemanticImplementationRequirement.Source SemanticImplementationRequirement.Language SemanticImplementationRequirement.File
            SemanticImplementationRequirement.ContentHash SemanticImplementationRequirement.ContextVersion SemanticImplementationRequirement.ResultVersion
            SemanticImplementationRequirement.RequiredCapability SemanticImplementationRequirement.IsAmbiguous SemanticImplementationRequirement.IsProvisional
            SemanticImplementationRequirement.AttachmentResolution SemanticImplementationRequirement.QueryShape
            """;
        var native = new SemanticSurfaceDisposition(SemanticSurfaceDispositionKind.Warned, "Conditional java method slots admit LF-only, SHA-256-matched inline command/reaction/query bodies with supported version-1 contracts; unplaced bodies retain STAGE-AXON-0403 or STAGE-AXON-IMPL-LANGUAGE diagnostics.");
        foreach (var member in nativeMembers.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)) entries[member] = native;
        var reducer = new SemanticSurfaceDisposition(SemanticSurfaceDispositionKind.Warned, "Conditional pure Java reducer transitions use the same LF-only, SHA-256-matched version-1 admission rules. Unplaced transitions retain STAGE-AXON-0306 and implementation diagnostics; dependent specifications remain disabled unless fully supported.");
        foreach (var member in new[] { "SemanticReducerTransition.RequirementId", "SemanticImplementationRole.ReducerTransition" }) entries[member] = reducer;
    }
}
