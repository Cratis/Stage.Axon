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
        var native = new SemanticSurfaceDisposition(SemanticSurfaceDispositionKind.Warned, "Conditional java method slots admit LF-only, SHA-256-matched inline command/reaction/query bodies with supported version-1 contracts. Selected unconsumed null/java requirements report STAGE-AXON-IMPL; other languages report STAGE-AXON-IMPL-LANGUAGE. Command result revision or slot resolution failure emits a throwing handler; this does not admit reducer bodies." + DegradationModes);
        foreach (var member in nativeMembers.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)) entries[member] = native;
        var reducer = new SemanticSurfaceDisposition(SemanticSurfaceDispositionKind.Warned, "STAGE-AXON-0306: reducer transitions have no native Java body slot; Java and foreign bodies remain unplaced and reducer specifications stay disabled.");
        foreach (var member in new[] { "SemanticReducerTransition.RequirementId", "SemanticImplementationRole.ReducerTransition" }) entries[member] = reducer;
    }
}
