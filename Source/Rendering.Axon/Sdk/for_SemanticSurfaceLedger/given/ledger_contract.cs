// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Screenplay.Semantics.Execution;

namespace Cratis.Stage.Rendering.Axon.Sdk.for_SemanticSurfaceLedger.given;

internal static class ledger_contract
{
    internal static IReadOnlyDictionary<string, Type> Types { get; } = new[] { typeof(ExecutableSemanticModel).Assembly, typeof(SemanticImplementationRequirement).Assembly }
        .Distinct().SelectMany(assembly => assembly.GetTypes()).Where(type => !type.IsNested && type.Namespace?.StartsWith("Cratis.Screenplay", StringComparison.Ordinal) == true)
        .GroupBy(type => type.Name, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, ModelType, StringComparer.Ordinal);

    // Some simple names exist in several namespaces (for example the execution availability enum); the ledger audits the model type.
    static Type ModelType(IGrouping<string, Type> group) =>
        group.Count() == 1 ? group.Single() : group.Single(type => type.Namespace == "Cratis.Screenplay.Semantics");

    internal static IEnumerable<KeyValuePair<string, SemanticSurfaceDisposition>> StrictEntries => SemanticSurfaceLedger.Entries;

    internal static IReadOnlyList<Type> Readers(SemanticSurfaceDisposition entry) => entry.Readers;

    internal static IReadOnlyList<string> Codes(SemanticSurfaceDisposition entry) => entry.Codes;

    internal static string? Reason(SemanticSurfaceDisposition entry) => entry.Reason?.ToString();
}
