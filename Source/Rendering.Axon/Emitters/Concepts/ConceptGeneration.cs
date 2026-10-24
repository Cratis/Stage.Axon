// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Concepts;

internal static class ConceptGeneration
{
    internal static void Emit(AxonEmissionContext context, SemanticConcept concept, JavaCodeBuilder body)
    {
        if (concept.Generation is not { } generation) return;
        var type = JavaNames.Type(concept.Name);
        if (string.Equals(generation.Kind, "uuid", StringComparison.OrdinalIgnoreCase) && concept.Primitive == SemanticPrimitiveType.Uuid)
        {
            body.Line($"public static {type} create() {{ return new {type}({context.BasePackage}.runtime.GenerateValue.uuid()); }}");
            return;
        }
        var path = concept.Id + "/generation";
        var intent = "generate " + concept.Name + " values using " + generation.Kind;
        context.Degradation.Unsupported(AxonRefusalCodes.Generation, concept.Id, "This value-generation strategy is not implemented by the Axon target.", intent, path);
        body.UnsupportedIntent(intent, path, AxonRefusalCodes.Generation)
            .Line($"public static {type} create() {JavaStubs.ThrowingBody(AxonRefusalCodes.Generation, intent)}");
    }
}
