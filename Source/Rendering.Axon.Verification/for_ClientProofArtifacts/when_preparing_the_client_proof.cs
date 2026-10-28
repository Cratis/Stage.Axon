// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Verification.for_ClientProofArtifacts;

public partial class when_preparing_the_client_proof : Specification
{
    [GeneratedRegex(@"\b(java|javax|jakarta|org\.axonframework|org\.springframework)\.[a-z]+\.[A-Za-z.]*[A-Z]", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex FrameworkType { get; }

    IReadOnlyDictionary<string, string> _java;
    IReadOnlyDictionary<string, string> _scenarios;
    string _javaAll;

    void Because()
    {
        _java = ClientProofArtifacts.JavaSources();
        _scenarios = ClientProofArtifacts.Scenarios();
        _javaAll = string.Join('\n', _java.Values);
    }

    [Fact]
    void should_include_the_java_tests() => _java.Keys.Order(StringComparer.Ordinal).SequenceEqual(["ClientContractTest.java", "ClientLiveLimitsTest.java", "ClientProofSupport.java"]).ShouldBeTrue();
    [Theory]
    [InlineData("ClientContractTest.java")]
    [InlineData("ClientLiveLimitsTest.java")]
    void should_replace_the_native_identity_provider(string name) => _java[name].ShouldContain("@TestBean(name = \"inMemoryUserDetailsManager\", methodName = \"users\")");

    [Fact] void should_include_the_scenarios() => _scenarios.Keys.ShouldContain("round-trip.ts");
    [Fact] void should_resolve_the_application_package() => _javaAll.ShouldNotContain("__APPLICATION__");
    [Fact] void should_resolve_the_runtime_package() => _javaAll.ShouldNotContain("__RUNTIME__");
    [Fact] void should_resolve_the_runtime_module_package() => _javaAll.ShouldNotContain("__RUNTIME_MODULE__");
    [Fact] void should_import_the_module_live_streams() => _java["ClientContractTest.java"].ShouldContain("import io.cratis.stage.axon.runtime.LiveStreams;");
    [Fact] void should_not_add_license_headers_to_java() => _javaAll.ShouldNotContain("Copyright (c) Cratis");
    [Fact] void should_bind_to_the_client_proof_package() => _java.Values.All(source => source.Contains("package corpusclientproof.runtime;", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_fail_when_node_cannot_start() => _java["ClientProofSupport.java"].ShouldContain("AssertionError");
    [Fact] void should_not_skip_when_node_is_missing() => _javaAll.ShouldNotContain("assumeTrue");
    [Fact]
    void should_use_imports_for_framework_types() => _javaAll.Split('\n')
        .Any(line => !line.StartsWith("import ", StringComparison.Ordinal) && !line.StartsWith("package ", StringComparison.Ordinal) && FrameworkType.IsMatch(line)).ShouldBeFalse();
    [Fact] void should_use_the_generated_client() => _scenarios["support.ts"].ShouldContain("createHttpTransport");
}
