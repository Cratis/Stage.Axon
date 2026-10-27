// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter.given;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Implementations.for_InlineJavaBodies;

public class when_resolving_a_java_body : a_view_request
{
    const string Body = "var text = \"java.time // \\\\ path\";\nvar block = \"\"\"\n  /* text */\n    java.time\n  \"\"\";\nreturn java.util.List.of();";
    SemanticImplementationRequirement requirement;
    AxonEmissionContext context;

    void Establish()
    {
        requirement = _request.ImplementationRequirements.Single(candidate => candidate.Role == SemanticImplementationRole.QueryPerformer) with
        {
            Language = "java",
            ContentHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Body)))
        };
        _request = _request with { ImplementationRequirements = [requirement], ImplementationContents = _request.ImplementationContents.SetItem(requirement.RequirementId, Body) };
        context = new(_request, new(_request.Profile));
    }

    [Fact]
    void should_resolve_exact_content()
    {
        Resolve(requirement).ShouldBeTrue();
        InlineJavaBodies.TryResolve(context, new(requirement.RequirementId, requirement.Role, requirement.Source.SemanticId), out var body, out _).ShouldBeTrue();
        body.ShouldEqual(Body);
    }

    [Fact] void should_reject_a_stale_hash() => Resolve(requirement with { ContentHash = "stale" }).ShouldBeFalse();
    [Fact] void should_reject_an_unknown_context() => Resolve(requirement with { ContextVersion = 2 }).ShouldBeFalse();
    [Fact] void should_reject_an_unknown_result() => Resolve(requirement with { ResultVersion = 2 }).ShouldBeFalse();
    [Fact] void should_reject_another_language() => Resolve(requirement with { Language = "csharp" }).ShouldBeFalse();
    [Fact] void should_require_the_exact_registered_language() => Resolve(requirement with { Language = "Java" }).ShouldBeFalse();
    [Fact] void should_reject_file_attachments() => Resolve(requirement with { File = "performer.java" }).ShouldBeFalse();
    [Fact] void should_reject_ambiguous_attachments() => Resolve(requirement with { IsAmbiguous = true }).ShouldBeFalse();
    [Fact] void should_reject_provisional_attachments() => Resolve(requirement with { IsProvisional = true }).ShouldBeFalse();
    [Fact] void should_reject_unknown_capabilities() => Resolve(requirement with { RequiredCapability = "unrecognized" }).ShouldBeFalse();
    [Fact] void should_reject_unresolved_attachments() => Resolve(requirement with { AttachmentResolution = SemanticAttachmentResolution.UnresolvedFile }).ShouldBeFalse();

    [Fact]
    void should_reject_missing_content()
    {
        var request = _request with { ImplementationContents = _request.ImplementationContents.Clear() };
        InlineJavaBodies.TryResolve(new(request, new(request.Profile)), new(requirement.RequirementId, requirement.Role, requirement.Source.SemanticId), out _, out _).ShouldBeFalse();
    }

    [Fact]
    void should_reject_the_wrong_role() => InlineJavaBodies.TryResolve(context, new(requirement.RequirementId, SemanticImplementationRole.CommandHandler, requirement.Source.SemanticId), out _, out _).ShouldBeFalse();

    [Fact]
    void should_name_the_authored_language_and_the_remedy()
    {
        var reason = Reason(requirement with { Language = "csharp" });
        reason.ShouldEqual("The Axon target only realizes Java bodies; language 'csharp' is not supported. Supply the body in Java (or render it with a target that realizes this language) and render again.");
    }

    [Fact]
    void should_report_an_unknown_language()
    {
        var reason = Reason(requirement with { Language = null });
        reason.ShouldEqual("The implementation language is unknown. Supply the body in Java and render again.");
    }

    string Reason(SemanticImplementationRequirement candidate)
    {
        var request = _request with { ImplementationRequirements = [candidate] };
        InlineJavaBodies.TryResolve(new(request, new(request.Profile)), new(candidate.RequirementId, candidate.Role, candidate.Source.SemanticId), out _, out var reason).ShouldBeFalse();
        return reason;
    }

    bool Resolve(SemanticImplementationRequirement candidate)
    {
        var request = _request with { ImplementationRequirements = [candidate] };
        return InlineJavaBodies.TryResolve(new(request, new(request.Profile)), new(candidate.RequirementId, candidate.Role, candidate.Source.SemanticId), out _, out _);
    }
}
