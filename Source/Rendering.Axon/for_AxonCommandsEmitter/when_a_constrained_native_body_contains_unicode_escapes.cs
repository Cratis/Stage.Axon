// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter;

public class when_a_constrained_native_body_contains_unicode_escapes : when_a_native_handler_is_constrained
{
    protected override string CreateBody(string append) => base.CreateBody(append) + "\n" + @"Event\u0041ppender.for\u0043ontext(context).append(" + append + ");";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void should_fail_closed_without_consuming_the_body(bool strict) => AssertRefused(strict, "enforce modeled uniqueness for every event a native command body appends");

    [Fact]
    void should_refuse_repeated_u_escapes() => Refuse(@"Event\uu0041ppender.for\uu0043ontext(context).append(null);");

    [Fact]
    void should_refuse_an_escape_in_a_comment() => Refuse(@"// \u0041");

    [Fact]
    void should_refuse_an_escape_in_a_string() => Refuse(@"var escaped = ""\u0041"";");

    [Fact]
    void should_accept_an_unconstrained_body_unchanged()
    {
        UseBody(@"/* native-constraint-body */ var escaped = ""\u0041"";");
        static SemanticFeature Feature(SemanticFeature feature) => feature with
        {
            Features = [.. feature.Features.Select(Feature)],
            Slices = [.. feature.Slices.Select(slice => slice with { Constraints = [] })]
        };
        var application = request.Model.Application with { Modules = [.. request.Model.Application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })] };
        request = request with { Model = ExecutableSemanticModel.Create(request.Model.LanguageVersion, request.Model.SemanticVersion, application) };
        var (context, code) = Emit(false);
        code.ShouldContain(body);
        code.ShouldNotContain(AxonRefusalCodes.Constraint);
        code.ShouldNotContain("UnsupportedOperationException");
        context.ConsumedImplementations.ShouldContain(requirementId);
        context.Degradation.Refusals.ShouldBeEmpty();
    }

    void Refuse(string content)
    {
        UseBody("/* native-constraint-body */\n" + content);
        AssertRefused(false, "enforce modeled uniqueness for every event a native command body appends");
        AssertRefused(true, "enforce modeled uniqueness for every event a native command body appends");
    }

    void UseBody(string content)
    {
        body = content;
        request = request with
        {
            ImplementationRequirements = [request.ImplementationRequirements[0] with { ContentHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content))) }],
            ImplementationContents = request.ImplementationContents.SetItem(requirementId, content)
        };
    }
}
