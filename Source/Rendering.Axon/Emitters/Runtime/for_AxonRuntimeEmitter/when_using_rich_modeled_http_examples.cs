// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter.given;
using Cratis.Stage.Rendering.Axon.Emitters.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Runtime.for_AxonRuntimeEmitter;

public class when_using_rich_modeled_http_examples : a_view_request
{
    AxonEmissionContext _context;
    SemanticReadModel _model;
    SemanticKeyedQuery _query;
    string _test;

    async Task Establish()
    {
        var loaded = await SemanticModelLoader.LoadFromPathAsync(Path.Combine(Root(), "Source/Rendering.Axon.Verification/Corpus/client-proof.play"), null, "RichExamples");
        _request = new(loaded.Model, loaded.Plan, AxonRendering.CreateProfile(), new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id));
        _context = new(_request, new(_request.Profile));
        _model = ScopeSelection.Select(_request).SelectMany(owner => owner.Slice.ReadModels).Single(model => model.Name == "WorkItemContacts");
        _query = ScopeSelection.Select(_request).SelectMany(owner => owner.Slice.Queries).Single(query => query.Name == "ContactByEmail");
    }

    void Because()
    {
        AxonHttpCardinalityTests.Emit(_context);
        _test = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/HttpOptionalQueryContractTest.java", StringComparison.Ordinal)));
    }

    [Fact] void should_use_the_modeled_email_for_the_request() => _test.ShouldContain(".param(\"email\", \"writer@example.test\")");
    [Fact] void should_use_the_same_email_in_the_rows() => _test.ShouldContain("writer@example.test");
    [Fact] void should_preserve_the_composite_example() => JsonSerializer.Serialize(HttpModeledExamples.Row(_context, _model)).ShouldContain("\"details\":{\"email\":\"writer@example.test\",\"title\":\"Valid\"}");
    [Fact] void should_preserve_the_enum_example() => JsonSerializer.Serialize(HttpModeledExamples.Row(_context, _model)).ShouldContain("\"state\":\"open\"");
    [Fact] void should_keep_the_two_row_assertion() => _test.ShouldContain("jsonPath(\"$.count\").value(2)");
    [Fact] void should_keep_the_absent_row_assertion() => _test.ShouldContain("status().isNotFound()");
    [Fact] void should_report_skipped_queries_without_complete_examples() => _context.Degradation.Warnings.ShouldContain(warning => warning.Code == "STAGE-AXON-HTTP-SPEC" && warning.Artifact != _query.Id);
}
