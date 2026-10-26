// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter.given;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Implementations.for_InlineJavaBodies;

public class when_a_reducer_body_has_no_supported_slot : a_view_request
{
    SemanticImplementationRequirement _requirement;
    AxonEmissionContext _context;
    const string Body = "return current;\n// java.time and indented bytes stay unchanged";

    void Establish()
    {
        _requirement = _request.ImplementationRequirements.First(candidate => candidate.Role == SemanticImplementationRole.ReducerTransition) with
        {
            Language = "java", File = null, ContentHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Body)))
        };
        _request = _request with { ImplementationRequirements = [_requirement], ImplementationContents = _request.ImplementationContents.SetItem(_requirement.RequirementId, Body) };
        _context = new(_request, new(_request.Profile));
    }

    [Fact]
    void should_admit_the_exact_body()
    {
        InlineJavaBodies.TryResolve(_context, new(_requirement.RequirementId, _requirement.Role, _requirement.Source.SemanticId), out var body, out _).ShouldBeTrue();
        body.ShouldEqual(Body);
        _context.ConsumedImplementations.Count.ShouldEqual(0);
    }

    [Fact] void should_require_the_pure_capability() => Resolve(_requirement with { RequiredCapability = "provider-defined" }).ShouldBeFalse();
    [Fact] void should_reject_foreign_languages() => Resolve(_requirement with { Language = "csharp" }).ShouldBeFalse();
    [Fact] void should_reject_stale_content() => Resolve(_requirement with { ContentHash = "stale" }).ShouldBeFalse();
    [Fact] void should_reject_unknown_contexts() => Resolve(_requirement with { ContextVersion = 2 }).ShouldBeFalse();
    [Fact] void should_reject_unknown_results() => Resolve(_requirement with { ResultVersion = 2 }).ShouldBeFalse();
    [Fact] void should_reject_file_content() => Resolve(_requirement with { File = "reducer.java" }).ShouldBeFalse();
    [Fact] void should_reject_ambiguity() => Resolve(_requirement with { IsAmbiguous = true }).ShouldBeFalse();
    [Fact] void should_reject_provisional_identity() => Resolve(_requirement with { IsProvisional = true }).ShouldBeFalse();
    [Fact] void should_reject_unresolved_identity() => Resolve(_requirement with { AttachmentResolution = SemanticAttachmentResolution.UnresolvedFile }).ShouldBeFalse();

    [Fact]
    void should_reject_missing_content()
    {
        var request = _request with { ImplementationContents = _request.ImplementationContents.Clear() };
        InlineJavaBodies.TryResolve(new(request, new(request.Profile)), new(_requirement.RequirementId, _requirement.Role, _requirement.Source.SemanticId), out _, out _).ShouldBeFalse();
    }

    [Fact]
    void should_reject_cr_even_with_a_matching_hash()
    {
        const string body = "return current;\r\n";
        var requirement = _requirement with { ContentHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body))) };
        var request = _request with { ImplementationRequirements = [requirement], ImplementationContents = _request.ImplementationContents.SetItem(requirement.RequirementId, body) };
        InlineJavaBodies.TryResolve(new(request, new(request.Profile)), new(requirement.RequirementId, requirement.Role, requirement.Source.SemanticId), out _, out _).ShouldBeFalse();
    }

    [Fact] void should_reject_the_wrong_role() => Resolve(_requirement with { Role = SemanticImplementationRole.QueryPerformer }).ShouldBeFalse();

    [Fact]
    void should_reject_the_wrong_source_identity() => Resolve(_requirement with { Source = _requirement.Source with { SemanticId = _request.Model.Application.Id } }).ShouldBeFalse();

    [Fact]
    void should_reject_the_wrong_owner_kind() => Resolve(_requirement with { Owner = SemanticAddress.ForCommand(_requirement.Owner, "Other") }).ShouldBeFalse();

    [Fact]
    void should_reject_duplicate_requirements()
    {
        var request = _request with { ImplementationRequirements = [_requirement, _requirement] };
        InlineJavaBodies.TryResolve(new(request, new(request.Profile)), new(_requirement.RequirementId, _requirement.Role, _requirement.Source.SemanticId), out _, out _).ShouldBeFalse();
    }

    [Fact]
    void should_reject_whitespace_even_with_a_matching_hash()
    {
        const string body = " \n  ";
        var requirement = _requirement with { ContentHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body))) };
        var request = _request with { ImplementationRequirements = [requirement], ImplementationContents = _request.ImplementationContents.SetItem(requirement.RequirementId, body) };
        InlineJavaBodies.TryResolve(new(request, new(request.Profile)), new(requirement.RequirementId, requirement.Role, requirement.Source.SemanticId), out _, out _).ShouldBeFalse();
    }

    bool Resolve(SemanticImplementationRequirement requirement)
    {
        var request = _request with { ImplementationRequirements = [requirement] };
        return InlineJavaBodies.TryResolve(new(request, new(request.Profile)), new(_requirement.RequirementId, _requirement.Role, _requirement.Source.SemanticId), out _, out _);
    }
}
