// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Sdk;

/// <summary>
/// Explicit contract with the published Stage semantic SDK. New members require a classification and implementation decision.
/// </summary>
internal static partial class SemanticSurfaceLedger
{
    static readonly LedgerBuilder _result = Build();

    internal static IReadOnlyDictionary<string, SemanticSurfaceDisposition> Entries => _result.Entries;

    internal static IReadOnlyList<string> Conflicts => _result.Conflicts;

    static LedgerBuilder Build()
    {
        var rows = new LedgerBuilder();
        ClassifyConditions(rows);
        ClassifyImplementations(rows);
        ClassifyViews(rows);
        ClassifyBehaviors(rows);
        ClassifyMetadata(rows);
        ClassifyModel(rows);
        ClassifyDomain(rows);
        ClassifyEvents(rows);
        ClassifyCommandMembers(rows);
        ClassifySecurity(rows);
        ClassifySpecifications(rows);
        return rows;
    }

    sealed class LedgerBuilder
    {
        internal Dictionary<string, SemanticSurfaceDisposition> Entries { get; } = new(StringComparer.Ordinal);

        internal List<string> Conflicts { get; } = [];

        internal void Rendered(string detail, string members, params Type[] readers) => Add(members, new(SemanticSurfaceDispositionKind.Rendered, detail, readers, [], null));

        internal void Warned(string[] codes, string detail, string members) => Add(members, new(SemanticSurfaceDispositionKind.Warned, detail, [], codes, null));

        internal void Ignored(IgnoredReason reason, string detail, string members) => Add(members, new(SemanticSurfaceDispositionKind.Ignored, detail, [], [], reason));

        void Add(string members, SemanticSurfaceDisposition disposition)
        {
            foreach (var key in members.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                if (!Entries.TryAdd(key, disposition)) Conflicts.Add(key);
        }
    }
}
