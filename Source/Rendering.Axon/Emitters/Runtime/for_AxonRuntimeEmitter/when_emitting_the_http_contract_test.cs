// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using System.Text.RegularExpressions;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Runtime.for_AxonRuntimeEmitter;

public partial class when_emitting_the_http_contract_test : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext _context;

    void Because()
    {
        _context = new AxonEmissionContext(request, new(request.Profile));
        new AxonRuntimeEmitter().Emit(_context);
    }

    [Theory]
    [InlineData("HttpContractTest")]
    [InlineData("HttpIdentityContractTest")]
    [InlineData("HttpBrowserContractTest")]
    void should_emit_the_template_under_src_test(string name) => _context.Artifacts.Exists(artifact => artifact.RelativePath.StartsWith("src/test/java/", StringComparison.Ordinal) && artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)).ShouldBeTrue();

    [Theory]
    [InlineData("HttpContractTest")]
    [InlineData("HttpIdentityContractTest")]
    [InlineData("HttpBrowserContractTest")]
    void should_import_its_nested_test_types(string name) => Source(name).ShouldContain("@Import(");

    [Theory]
    [InlineData("HttpContractTest")]
    [InlineData("HttpIdentityContractTest")]
    [InlineData("HttpBrowserContractTest")]
    void should_not_dirty_the_shared_context(string name) => Source(name).ShouldNotContain("DirtiesContext");

    [Theory]
    [InlineData("HttpContractTest")]
    [InlineData("HttpIdentityContractTest")]
    [InlineData("HttpBrowserContractTest")]
    void should_not_qualify_types_in_its_body(string name)
    {
        var body = string.Join('\n', Source(name).Split('\n').Where(line => !line.StartsWith("package ", StringComparison.Ordinal) && !line.StartsWith("import ", StringComparison.Ordinal)));
        QualifiedTypes.Matches(Strings.Replace(body, "\"\"")).ShouldBeEmpty();
    }

    [GeneratedRegex("\"(?:\\\\.|[^\"\\\\])*\"", RegexOptions.None, 1000)]
    private static partial Regex Strings { get; }

    [GeneratedRegex(@"(?<![\w.])(?:java|javax|jakarta|org|io)\.[a-z0-9_.]*\.[A-Z]\w*", RegexOptions.None, 1000)]
    private static partial Regex QualifiedTypes { get; }

    string Source(string name) => Encoding.UTF8.GetString(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)).Bytes.AsSpan());
}
