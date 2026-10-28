// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Admission;

namespace Cratis.Stage.Rendering.Axon.Emitters;

internal static partial class UnsupportedSummary
{
    internal static void Emit(AxonEmissionContext context)
    {
        var locations = Locations(context.Request.Model.Application);
        var rows = context.Degradation.UnsupportedConstructs.Zip(context.Degradation.Intents)
            .Select(pair => Resolve(context, locations, pair.First, pair.Second)).Distinct()
            .OrderBy(row => row.Code, StringComparer.Ordinal).ThenBy(row => row.Location, StringComparer.Ordinal)
            .ThenBy(row => row.File, StringComparer.Ordinal).ThenBy(row => row.Intent, StringComparer.Ordinal).ThenBy(row => row.Reason, StringComparer.Ordinal).ToArray();
        var text = new StringBuilder("# Unsupported Screenplay constructs\n\nThe application can build and start, but the following modeled behavior is not implemented. Disabled specifications are not proof that the behavior works.\n\n");
        text.Append("## Summary\n\n");
        if (rows.Length == 0) text.Append("No unsupported constructs.\n");
        WriteSummary(text, rows);
        if (context.Degradation.Notes.Count > 0) text.Append("\n## Notes\n\n").AppendJoin("\n\n", context.Degradation.Notes).Append('\n');
        WriteDetails(text, rows);
        context.Artifacts.Add(PlannedArtifact.CreateText("UNSUPPORTED.md", text.ToString()));
    }

    static void WriteSummary(StringBuilder text, Row[] rows)
    {
        foreach (var group in rows.GroupBy(row => row.Code))
        {
            var description = Degradations.Find(group.Key);
            text.Append("- `").Append(Cell(group.Key)).Append("` (").Append(group.Count()).Append("): ")
                .Append(Cell(description?.Construct ?? "render the modeled construct")).Append('.');
            if (description?.AxonDocs is { } docs) text.Append(" See ").Append(docs);
            text.Append('\n');
        }
    }

    static void WriteDetails(StringBuilder text, Row[] rows)
    {
        if (rows.Length > 0)
        {
            text.Append("\n## Details\n\n| Code | Model location | Generated file | Intent | Reason |\n| --- | --- | --- | --- | --- |\n");
            foreach (var row in rows)
            {
                text.Append("| ").Append(Cell(row.Code)).Append(" | ").Append(Cell(row.Location)).Append(" | `").Append(Cell(row.File))
                    .Append("` | ").Append(Cell(row.Intent)).Append(" | ").Append(Cell(row.Reason)).Append(" |\n");
            }
        }
    }

    static Row Resolve(AxonEmissionContext context, IReadOnlyDictionary<SemanticId, string> locations, ArtifactRenderDiagnostic diagnostic, AxonUnsupportedIntent intent)
    {
        try
        {
            var location = Location(locations, intent);
            var file = GeneratedFile(context, intent.Construct);
            return new(intent.Code, location, file, Intent(intent, diagnostic.Message, location), diagnostic.Message);
        }
        catch (Exception)
        {
            var location = string.IsNullOrEmpty(intent.ModelPath) ? intent.Construct.ToString() : intent.ModelPath;
            return new(intent.Code, location, "—", Intent(intent, diagnostic.Message, "modeled construct"), diagnostic.Message);
        }
    }

    static string GeneratedFile(AxonEmissionContext context, SemanticId construct) => context.Artifacts
        .Where(artifact => !artifact.Sources.IsDefaultOrEmpty && artifact.Sources.Contains(construct) && artifact.RelativePath.EndsWith(".java", StringComparison.Ordinal))
        .OrderBy(artifact => artifact.RelativePath.StartsWith("src/main/", StringComparison.Ordinal) ? 0 : 1)
        .ThenBy(artifact => artifact.RelativePath, StringComparer.Ordinal).FirstOrDefault()?.RelativePath ?? "—";

    static string Intent(AxonUnsupportedIntent intent, string reason, string location)
    {
        if (!string.IsNullOrWhiteSpace(intent.Intent) && !string.Equals(Cell(intent.Intent), Cell(reason), StringComparison.Ordinal)) return intent.Intent;
        var fallback = Degradations.Find(intent.Code)?.Construct ?? ("render the " + location);
        return string.Equals(Cell(fallback), Cell(reason), StringComparison.Ordinal) ? "Requested behavior: " + fallback : fallback;
    }

    static string Location(IReadOnlyDictionary<SemanticId, string> locations, AxonUnsupportedIntent intent)
    {
        var id = intent.Construct.ToString();
        if (!locations.TryGetValue(intent.Construct, out var location)) return string.IsNullOrEmpty(intent.ModelPath) ? id : intent.ModelPath;
        if (string.IsNullOrEmpty(intent.ModelPath) || intent.ModelPath == id) return location;
        var detail = intent.ModelPath.StartsWith(id + "/", StringComparison.Ordinal) ? intent.ModelPath[(id.Length + 1)..] : intent.ModelPath;
        return location + " (" + detail + ")";
    }

    static string Cell(string value) => value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');

    sealed record Row(string Code, string Location, string File, string Intent, string Reason);
}
