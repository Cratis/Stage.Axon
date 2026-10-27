// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Rendering.Axon.Sdk.for_SemanticSurfaceLedger.given;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Sdk.for_SemanticSurfaceLedger;

public class when_verifying_warned_members
{
    [Fact]
    public void should_verify_codes_against_production_source()
    {
        var entries = ledger_contract.StrictEntries.Where(entry => entry.Value.Kind == SemanticSurfaceDispositionKind.Warned).ToArray();
        var codes = entries.SelectMany(entry => ledger_contract.Codes(entry.Value).Select(code => (entry.Key, entry.Value.Detail, Code: code))).ToArray();
        var missing = entries.Where(entry => ledger_contract.Codes(entry.Value).Count == 0).Select(entry => entry.Key + ": no codes");
        var unknown = codes.Where(item => !production_source.Codes.Contains(item.Code)).Select(item => item.Key + ": not a production code: " + item.Code);
        var omitted = codes.Where(item => !item.Detail.Contains(item.Code, StringComparison.Ordinal)).Select(item => item.Key + ": detail omits " + item.Code);
        var failures = missing.Concat(unknown).Concat(omitted).ToArray();
        Assert.True(failures.Length == 0, string.Join('\n', failures));
        Assert.NotEmpty(codes);
    }
}
