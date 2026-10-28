// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Verification.for_ConstraintProofArtifacts;

public partial class when_writing_the_constraint_proof : Specification
{
    [GeneratedRegex(@"\b(?:[a-z][a-z0-9_]*\.)+[A-Z][A-Za-z0-9_]*", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex QualifiedType { get; }

    IReadOnlyDictionary<string, string> sources;
    string source;

    void Because()
    {
        sources = ConstraintProofArtifacts.JavaSources();
        source = sources["GeneratedNativeUniqueTest.java"];
    }

    [Fact] void should_find_the_native_proof() => sources.Keys.SequenceEqual(["GeneratedNativeUniqueTest.java"]).ShouldBeTrue();
    [Fact] void should_resolve_placeholders() => source.ShouldNotContain("__");
    [Fact] void should_use_the_application_package() => source.ShouldContain("package corpusconstrainedjavabodies.runtime;");
    [Fact] void should_import_the_runtime_conflict() => source.ShouldContain("import io.cratis.stage.axon.runtime.UniqueValueConflict;");
    [Fact] void should_dispatch_through_the_gateway() => source.ShouldContain("gateway.send(command, Void.class).get(10, TimeUnit.SECONDS)");
    [Fact] void should_repeat_eight_contenders_twenty_five_times() => (source.Contains("CONTENDERS = 8", StringComparison.Ordinal) && source.Contains("REPETITIONS = 25", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_bound_the_start_gate() => source.ShouldContain("latch.await(5, TimeUnit.SECONDS)");
    [Fact] void should_prove_swallowed_conflicts() => source.ShouldContain("void swallowedConflictStillFails()");
    [Fact] void should_not_disable_native_tests() => source.ShouldNotContain("@Disabled");
    [Fact]
    void should_import_all_qualified_types() => source.Split('\n')
        .Any(line => !line.StartsWith("import ", StringComparison.Ordinal) && !line.StartsWith("package ", StringComparison.Ordinal) && QualifiedType.IsMatch(line)).ShouldBeFalse();
}
