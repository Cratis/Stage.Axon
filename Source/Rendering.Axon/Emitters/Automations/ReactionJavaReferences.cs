// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Automations;

/// <summary>Registers types in renderer-produced expressions, without changing string literals.</summary>
internal static class ReactionJavaReferences
{
    internal static void Claim(JavaCodeBuilder code, IEnumerable<string> expressions) => code.ClaimExpressions(expressions);

    internal static string Expression(JavaCodeBuilder code, string expression) => code.ReferenceExpression(expression);
}
