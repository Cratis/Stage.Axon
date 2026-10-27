// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Concepts;

internal static class ConceptGenerationOptions
{
    internal static void Report(AxonEmissionContext context, SemanticConcept concept, JavaCodeBuilder body)
    {
        const string code = "STAGE-AXON-GENERATION-OPTIONS";
        var path = concept.Id + "/generation";
        var intent = "generate " + concept.Name + " values using declared UUID options";
        context.Degradation.Unsupported(code, concept.Id, "Generation options are not represented by UUID value creation.", intent, path);
        body.UnsupportedIntent(intent, path, code);
    }
}
