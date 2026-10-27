// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Verification.for_ClientProofApplication;

public class when_planning_the_client_proof_application : Specification
{
    ArtifactRenderPlan _plan;

    async Task Establish()
    {
        var repository = for_CorpusApplications.given.the_repository.Root();
        var compilation = await SemanticModelLoader.LoadAsync(Path.Combine(repository, "Source/Rendering.Axon.Verification/Corpus"), ["client-proof.play"], null, "CorpusClientProof");
        compilation.Success.ShouldBeTrue();
        _plan = AxonRendering.PlanFrom(compilation.Loaded!, new(ArtifactRenderScopeKind.Application, compilation.Loaded!.Model.Application.Id), default, CorpusRenderer.Profile);
    }

    [Fact] void should_plan_without_refusals() => (_plan.Success && _plan.Refusals.IsEmpty).ShouldBeTrue();
    [Fact] void should_allow_at_most_one_row_for_the_optional_query() => Source("WorkItemByIdHandler").ShouldContain("QueryResults.atMostOne(rows)");
    [Fact] void should_stream_the_optional_query() => Source("WorkItemByIdEndpoint").ShouldContain("TEXT_EVENT_STREAM_VALUE");
    [Fact] void should_not_stream_the_by_name_query() => Source("WorkItemByNameEndpoint").ShouldNotContain("TEXT_EVENT_STREAM_VALUE");
    [Fact] void should_return_the_record_from_the_create_command() => Source("CreateWorkItemHandler").ShouldContain("return new CreateWorkItemResponse(");
    [Fact] void should_return_nothing_from_the_rename_command() => Source("RenameWorkItemEndpoint").ShouldContain("CompletableFuture<Void>");
    [Fact] void should_claim_the_unique_name() => Source("ModeledConstraints").ShouldContain("UniqueWorkItemName");
    [Fact] void should_render_the_string_enum() => Source("WorkItemState").ShouldContain("INPROGRESS(\"inProgress\")");
    [Fact] void should_render_the_numbered_enum() => Source("WorkItemPriority").ShouldContain("HIGH(10)");
    [Fact] void should_render_the_client() => _plan.Artifacts.Any(artifact => artifact.RelativePath == "client/src/work/items/overview.ts").ShouldBeTrue();


    string Source(string name) => Encoding.UTF8.GetString(_plan.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)).Bytes.AsSpan());
}
