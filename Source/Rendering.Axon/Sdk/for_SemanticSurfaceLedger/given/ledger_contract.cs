// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Screenplay.Semantics.Execution;

namespace Cratis.Stage.Rendering.Axon.Sdk.for_SemanticSurfaceLedger.given;

internal static class ledger_contract
{
    internal const int LegacyBudget = 162;

    internal static IReadOnlyDictionary<string, Type> Types { get; } = new[] { typeof(ExecutableSemanticModel).Assembly, typeof(SemanticImplementationRequirement).Assembly }
        .Distinct().SelectMany(assembly => assembly.GetTypes()).Where(type => !type.IsNested && type.Namespace?.StartsWith("Cratis.Screenplay", StringComparison.Ordinal) == true)
        .ToDictionary(type => type.Name, type => type, StringComparer.Ordinal);

    internal static IEnumerable<KeyValuePair<string, SemanticSurfaceDisposition>> StrictEntries => SemanticSurfaceLedger.Entries.Where(entry => !Legacy(entry.Value));

    internal static IReadOnlyList<Type> Readers(SemanticSurfaceDisposition entry) => entry.Readers;

    internal static IReadOnlyList<string> Codes(SemanticSurfaceDisposition entry) => entry.Codes;

    internal static string? Reason(SemanticSurfaceDisposition entry) => entry.Reason?.ToString();

    internal static bool Legacy(SemanticSurfaceDisposition entry) => entry.IsLegacy;
}
