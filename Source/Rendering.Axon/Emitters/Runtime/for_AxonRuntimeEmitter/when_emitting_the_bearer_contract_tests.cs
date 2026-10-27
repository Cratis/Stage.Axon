// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using System.Text.RegularExpressions;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Runtime.for_AxonRuntimeEmitter;

public partial class when_emitting_the_bearer_contract_tests : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext context;
    AxonEmissionContext basic;

    void Because()
    {
        var bearer = request with { Profile = AxonRendering.WithBearerTokens(request.Profile) };
        context = new AxonEmissionContext(bearer, new(bearer.Profile));
        basic = new AxonEmissionContext(request, new(request.Profile));
        new AxonRuntimeEmitter().Emit(context);
        new AxonRuntimeEmitter().Emit(basic);
    }

    [Theory]
    [InlineData("HttpBearerContractTest")]
    [InlineData("HttpBearerUnconfiguredTest")]
    [InlineData("HttpBearerTokens")]
    void should_emit_only_in_bearer_mode_under_src_test(string name)
    {
        context.Artifacts.Exists(artifact => artifact.RelativePath.StartsWith("src/test/java/", StringComparison.Ordinal) && artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)).ShouldBeTrue();
        basic.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("HttpIdentityContractTest")]
    [InlineData("HttpBrowserContractTest")]
    void should_not_emit_basic_contracts(string name) => context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)).ShouldBeFalse();

    [Theory]
    [InlineData("HttpBearerContractTest")]
    [InlineData("HttpBearerUnconfiguredTest")]
    void should_import_its_nested_test_types(string name) => Source(name).ShouldContain("@Import(");

    [Theory]
    [InlineData("HttpBearerContractTest")]
    [InlineData("HttpBearerUnconfiguredTest")]
    [InlineData("HttpBearerTokens")]
    void should_not_dirty_the_shared_context(string name) => Source(name).ShouldNotContain("DirtiesContext");

    [Theory]
    [InlineData("HttpBearerContractTest")]
    [InlineData("HttpBearerUnconfiguredTest")]
    [InlineData("HttpBearerTokens")]
    void should_not_qualify_types_in_its_body(string name)
    {
        var body = string.Join('\n', Source(name).Split('\n').Where(line => !line.StartsWith("package ", StringComparison.Ordinal) && !line.StartsWith("import ", StringComparison.Ordinal)));
        QualifiedTypes.Matches(Strings.Replace(body, "\"\"")).ShouldBeEmpty();
    }

    [Fact]
    void should_preserve_every_emitted_template_import()
    {
        const string prefix = "Cratis.Stage.Rendering.Axon.Emitters.Runtime.Templates.";
        var assembly = typeof(AxonRuntimeEmitter).Assembly;
        foreach (var resource in assembly.GetManifestResourceNames().Where(name => name.StartsWith(prefix, StringComparison.Ordinal)))
        {
            if (resource.EndsWith(".test.HttpIdentityContractTest.java", StringComparison.Ordinal)) continue;
            if (resource.EndsWith(".test.HttpBrowserContractTest.java", StringComparison.Ordinal)) continue;
            using var stream = assembly.GetManifestResourceStream(resource);
            using var reader = new StreamReader(stream);
            var imports = reader.ReadToEnd().Replace("__RUNTIME__", context.BasePackage + ".runtime", StringComparison.Ordinal)
                .Replace("__SECURITY__", context.BasePackage + ".infrastructure", StringComparison.Ordinal)
                .Split('\n').Where(line => line.StartsWith("import ", StringComparison.Ordinal));
            var name = resource[(resource.LastIndexOf('.', resource.Length - ".java".Length - 1) + 1)..^".java".Length];
            var emitted = Source(name).Split('\n').Where(line => line.StartsWith("import ", StringComparison.Ordinal));
            emitted.SequenceEqual(imports.Order(StringComparer.Ordinal)).ShouldBeTrue();
        }
    }

    [GeneratedRegex("\"(?:\\\\.|[^\"\\\\])*\"", RegexOptions.None, 1000)]
    private static partial Regex Strings { get; }

    [GeneratedRegex(@"(?<![\w.])(?:java|javax|jakarta|org|io)\.[a-z0-9_.]*\.[A-Z]\w*", RegexOptions.None, 1000)]
    private static partial Regex QualifiedTypes { get; }

    string Source(string name) => Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)).Bytes.AsSpan());
}
