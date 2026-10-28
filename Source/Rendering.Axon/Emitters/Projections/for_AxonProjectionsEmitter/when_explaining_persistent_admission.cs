// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_explaining_persistent_admission : given.a_view_request
{
    AxonEmissionContext _context;

    void Because()
    {
        _context = new(_request, new(_request.Profile));
        new AxonProjectionsEmitter().Emit(_context);
    }

    [Fact] void should_describe_durable_flat_rows() => Message("Total").ShouldContain("stores this read model durably");
    [Fact] void should_describe_durable_contextual_rows() => Message("ContextTotal").ShouldContain("stores this read model durably");
    [Fact] void should_describe_durable_reducer_rows() => Message("RebuiltTotal").ShouldContain("stores this read model durably");
    [Fact] void should_not_claim_flat_only_admission() => new[] { "Total", "ContextTotal", "RebuiltTotal" }.ShouldEachConformTo(name => !Message(name).Contains("flat projections only", StringComparison.Ordinal));
    [Fact] void should_not_claim_scoped_or_reducer_refusal_for_admitted_models() => new[] { "Total", "ContextTotal", "RebuiltTotal" }.ShouldEachConformTo(name => !Message(name).Contains("scoped/reducer", StringComparison.Ordinal));
    [Fact] void should_explain_contextual_store_admission() => Code("ContextTotalStoreConfiguration").ShouldContain("stores this read model durably");
    [Fact] void should_explain_reducer_store_admission() => Code("RebuiltTotalStoreConfiguration").ShouldContain("stores this read model durably");
    [Fact] void should_explain_persistent_scope_refusal() => Message("Board").ShouldContain("Persistent startup refuses this read model because");
    [Fact] void should_name_the_stateful_scope() => Message("Board").ShouldContain("stateful scope");
    [Fact] void should_name_the_composite_key() => Message("Metric").ShouldContain("composite");
    [Fact] void should_also_name_row_removal() => Message("Metric").ShouldContain("removes rows");
    [Fact] void should_put_the_reason_in_the_refused_store_comment() => Code("BoardStoreConfiguration").ShouldContain("stateful scope");

    string Message(string name) => _context.Degradation.UnsupportedConstructs.Concat(_context.Degradation.Warnings).Single(diagnostic => diagnostic.Artifact == ReadModelId(name) && diagnostic.Code == "STAGE-AXON-0308").Message;
    string Code(string name) => Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith('/' + name + ".java", StringComparison.Ordinal)));
}
