// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Cratis.Screenplay.Semantics;
using Cratis.Screenplay.Semantics.Execution;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Sdk.for_SemanticSurfaceLedger.given;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Sdk.for_SemanticSurfaceLedger;

public class when_verifying_rendered_members
{
    // Every event is emitted private; only public visibility takes a special unsupported branch.
    static readonly IReadOnlySet<string> RenderedByDefault = new HashSet<string>(StringComparer.Ordinal) { "SemanticEventVisibility.Private" };

    static IEnumerable<KeyValuePair<string, SemanticSurfaceDisposition>> Entries => ledger_contract.StrictEntries.Where(entry => entry.Value.Kind == SemanticSurfaceDispositionKind.Rendered);

    [Fact]
    public void should_name_production_readers_for_every_rendered_member()
    {
        var missing = Entries.Where(entry => ledger_contract.Readers(entry.Value).Count == 0).Select(entry => entry.Key + ": no readers");
        var invalid = Entries.SelectMany(entry => ledger_contract.Readers(entry.Value).Where(reader => !IsProductionReader(reader))
            .Select(reader => entry.Key + ": not a production reader: " + reader.FullName));
        var failures = missing.Concat(invalid).ToArray();
        Assert.True(failures.Length == 0, string.Join('\n', failures));
    }

    [Fact]
    public void should_verify_every_property_read_in_il()
    {
        var failures = new List<string>();
        var checkedCount = 0;
        var properties = Entries.Where(entry => !entry.Key.EndsWith(".$type", StringComparison.Ordinal) && !ledger_contract.Types[entry.Key.Split('.')[0]].IsEnum).ToArray();
        foreach (var (key, entry) in properties)
        {
            var parts = key.Split('.');
            foreach (var reader in ledger_contract.Readers(entry))
                if (!il_reads.Reads(reader, ledger_contract.Types[parts[0]], parts[1])) failures.Add(key + ": getter not read by " + reader.FullName);
            checkedCount++;
        }
        Assert.True(failures.Count == 0, string.Join('\n', failures));
        Assert.Equal(properties.Length, checkedCount);
        Assert.True(checkedCount > 0, "The IL property-read check must not be vacuous.");
    }

    [Fact]
    public void should_verify_enum_and_type_claims_in_reader_source()
    {
        var entries = Entries.Where(entry => IsEnumOrMarker(entry.Key) && !RenderedByDefault.Contains(entry.Key));
        var failures = entries.SelectMany(entry => ledger_contract.Readers(entry.Value).Where(reader => !NamesLiteral(reader, entry.Key))
            .Select(reader => entry.Key + ": literal missing from " + reader.FullName)).ToArray();
        Assert.True(failures.Length == 0, string.Join('\n', failures));
    }

    [Fact]
    public void should_find_a_confirmed_command_name_read() => Assert.True(il_reads.Reads(typeof(CommandContractsEmitter), typeof(SemanticCommand), "Name"));

    [Fact]
    public void should_not_invent_a_body_lines_read() => Assert.False(il_reads.Reads(typeof(AxonCommandsEmitter), typeof(SemanticImplementationRequirement), "BodyLines"));

    static bool IsProductionReader(Type reader)
    {
        string[] excluded = [".Admission", ".for_", ".given"];
        if (reader.Assembly != typeof(SemanticSurfaceLedger).Assembly) return false;
        if (reader.Name.StartsWith("SemanticSurfaceLedger", StringComparison.Ordinal)) return false;
        return !excluded.Any(name => reader.Namespace?.Contains(name, StringComparison.Ordinal) == true);
    }

    static bool IsEnumOrMarker(string key) => ledger_contract.Types[key.Split('.')[0]].IsEnum || key.EndsWith(".$type", StringComparison.Ordinal);

    static bool NamesLiteral(Type reader, string key)
    {
        var literal = key.EndsWith(".$type", StringComparison.Ordinal) ? key.Split('.')[0] : key;
        return production_source.FilesOf(reader).Any(text => Regex.IsMatch(text, @"\b" + Regex.Escape(literal) + @"\b", RegexOptions.NonBacktracking));
    }
}
