// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Rendering.Axon.Sdk.for_SemanticSurfaceLedger.given;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Sdk.for_SemanticSurfaceLedger;

public class when_verifying_ignored_members
{
    static readonly string[] Advisory =
    [
        "AffectedInstanceCardinality.Many", "AffectedInstanceCardinality.One", "AffectedInstanceCardinality.ZeroOrOne",
        "SemanticAffectedInstance.Cardinality", "SemanticProjection.Target", "SemanticReducer.Name"
    ];

    [Fact]
    public void should_explain_ignored_members_with_closed_reasons()
    {
        var failures = new List<string>();
        var advisory = new List<string>();
        foreach (var (key, entry) in ledger_contract.StrictEntries.Where(entry => entry.Value.Kind == SemanticSurfaceDispositionKind.Ignored))
        {
            var reason = ledger_contract.Reason(entry);
            if (reason is null || string.IsNullOrWhiteSpace(entry.Detail)) failures.Add(key + ": missing reason or detail");
            if (reason == "UnknownSentinel" && !key.EndsWith(".Unknown", StringComparison.Ordinal)) failures.Add(key + ": not an Unknown sentinel");
            if (reason == "Advisory") advisory.Add(key);
        }
        Assert.True(failures.Count == 0, string.Join('\n', failures));
        Assert.Equal(Advisory.Order(), advisory.Order());
    }

    [Fact]
    public void should_not_retain_blanket_or_placeholder_details()
    {
        string[] forbidden = ["UNCLASSIFIED", "UNREAD-BEHAVIORAL", "scaffold-only", "Read by the Axon emitters under"];
        var failures = ledger_contract.StrictEntries.Where(entry => forbidden.Any(text => entry.Value.Detail.Contains(text, StringComparison.Ordinal))).Select(entry => entry.Key).ToArray();
        Assert.True(failures.Length == 0, string.Join('\n', failures));
    }

    [Fact]
    public void should_never_render_the_22_confirmed_unread_members()
    {
        const string unread = "SemanticGeneration.Case SemanticGeneration.Size SemanticGeneration.Alphabet SemanticGeneration.Pattern SemanticGeneration.Checksum SemanticValidationRuleKind.Checksum SemanticValidationRuleKind.RulePredicate SemanticValidationRuleKind.CodeValidation SemanticValidationRuleKind.DecimalPlaces SemanticValidationSeverity.Information SemanticImplementationRequirement.BodyLines SemanticAttachmentResolution.UnresolvedFile SemanticAttachmentResolution.Pending SemanticExecutionCapability.ImplementationBody SemanticExecutionCapability.IdentityAllocation SemanticExpressionKind.ReadPath SemanticEventContextValueKind.CausedByName SemanticEventContextValueKind.CausedByUserName SemanticValueKind.Null SemanticClaimTargetKind.Subject SemanticReadPathExpression.PropertyId SemanticPerformedQueryArgumentKind.Filter";
        Assert.Equal(22, unread.Split(' ').Length);
        Assert.All(unread.Split(' '), key => Assert.NotEqual(SemanticSurfaceDispositionKind.Rendered, SemanticSurfaceLedger.Entries[key].Kind));
    }
}
