// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;

namespace Cratis.Stage.Rendering.Axon.Emitters.Implementations;

internal static class UnimplementedValidation
{
    internal const string Reason = "The validation rule has no Java implementation; the generated validator accepts every value.";

    internal static string Report(AxonEmissionContext context, SemanticId owner, string intent, string path)
    {
        context.Degradation.Unsupported(AxonRefusalCodes.ImplementationValidation, owner, Reason, intent, path);
        return Reason;
    }
}
