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
}
