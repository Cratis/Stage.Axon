// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Rendering.Axon.Sdk.for_SemanticSurfaceLedger.given;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Sdk.for_SemanticSurfaceLedger;

public class when_describing_part_key_persistence
{
    [Fact]
    public void should_describe_conditional_part_key_support()
    {
        var detail = ledger_contract.StrictEntries.Single(entry => entry.Key == "SemanticReadModel.Properties").Value.Detail;
        Assert.Contains("nonempty root parts keys", detail);
        Assert.Contains("top-level part-key removals", detail);
        Assert.Contains("string keys and values", detail);
        Assert.Contains("Stateful scopes and named composite keys remain outside persistence", detail);
        Assert.Contains("STAGE-AXON-0308", detail);
    }
}
