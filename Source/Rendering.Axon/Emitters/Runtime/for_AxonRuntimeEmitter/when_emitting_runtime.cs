// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Admission;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Runtime.for_AxonRuntimeEmitter;

public class when_emitting_runtime : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext context;

    void Because()
    {
        context = new(request, new AxonDegradation(request.Profile));
        new AxonRuntimeEmitter().Emit(context);
    }

    [Fact]
    void should_emit_every_runtime_file_in_the_agreed_package() =>
        context.Artifacts.TrueForAll(artifact => artifact.RelativePath.Contains($"/java/{context.BasePackage}/runtime/", StringComparison.Ordinal)).ShouldBeTrue();

    [Fact]
    void should_emit_primitive_jackson_support_in_its_own_package() =>
        context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/runtime/json/ConceptsModule.java", StringComparison.Ordinal)).RelativePath.ShouldContain("src/main/java/");

    [Fact]
    void should_keep_test_support_out_of_the_application_sources() =>
        context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/TestSupport.java", StringComparison.Ordinal)).RelativePath.ShouldContain("src/test/java/");

    [Fact]
    void should_replace_every_package_placeholder() =>
        context.Artifacts.Exists(artifact => Encoding.UTF8.GetString(artifact.Bytes.AsSpan()).Contains("__RUNTIME__", StringComparison.Ordinal)).ShouldBeFalse();

    [Fact] void should_emit_the_inert_persistent_safety_helper() => context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/persistent/PersistentReadModelSafety.java", StringComparison.Ordinal)).RelativePath.ShouldContain("src/main/java/");
    [Fact] void should_emit_ordinary_native_safety_tests() => context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/PersistentReadModelSafetyTest.java", StringComparison.Ordinal)).RelativePath.ShouldContain("src/test/java/");
    [Fact]
    void should_register_installed_axon_entities_in_the_test_fixture()
    {
        var fixture = Template("PersistentProbeSupport.java");
        fixture.ShouldContain("@EntityScan(basePackageClasses = { UniqueClaim.class,\n            AggregateEventEntry.class,\n            TokenEntry.class })");
        fixture.ShouldContain($"import {context.BasePackage}.runtime.persistent.UniqueClaim;");
        fixture.ShouldContain("import org.axonframework.eventsourcing.eventstore.jpa.AggregateEventEntry;");
        fixture.ShouldContain("import org.axonframework.messaging.eventhandling.processing.streaming.token.store.jpa.TokenEntry;");
    }

    [Fact]
    void should_not_emit_hand_rolled_concept_types()
    {
        foreach (var name in new[] { "ConceptAs", "Generatable", "GeneratableInContext", "GenerateValue", "GenerationContext", "ValueEncoding" })
        {
            context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith($"/runtime/{name}.java", StringComparison.Ordinal)).ShouldBeFalse();
        }
    }

    [Fact]
    void should_keep_the_compat_concepts_module() => Template("ConceptsModule.java").ShouldContain("import io.cratis.fundamentals.concepts.ConceptAs;");

    [Fact]
    void should_keep_the_exact_serializer_encodings()
    {
        var serializer = Template("ConceptSerializer.java");
        serializer.ShouldContain("String.valueOf(concept.value())");
        serializer.ShouldContain("context.writeValue(generator, concept.value())");
    }

    [Fact]
    void should_pin_exact_wire_tests()
    {
        var tests = Template("RuntimeTest.java");
        tests.ShouldContain("void exponentDecimalValueKeepsItsText()");
        tests.ShouldContain("void exponentDecimalMapKeyKeepsItsText()");
        tests.ShouldContain("void decimalKeysRoundTrip()");
        tests.ShouldContain("void utcTimestampsKeepZ()");
        tests.ShouldContain("1E+2");
        tests.ShouldContain("2026-10-24T12:00:00Z");
    }

    [Fact]
    void should_pin_the_application_mapper_wire_test()
    {
        var tests = Template("RuntimeHttpRoundTripTest.java");
        tests.ShouldContain("void exactConceptWireFormatSurvivesHttpRoundTrip()");
        tests.ShouldContain("1E+2");
        tests.ShouldContain("2026-10-24T12:00:00Z");
        tests.ShouldContain("content().string(json)");
    }

    [Fact]
    void should_sort_every_emitted_import_block_after_package_substitution()
    {
        foreach (var artifact in context.Artifacts)
        {
            var imports = Encoding.UTF8.GetString(artifact.Bytes.AsSpan()).Split('\n').Where(line => line.StartsWith("import ", StringComparison.Ordinal)).ToArray();
            imports.SequenceEqual(imports.Order(StringComparer.Ordinal)).ShouldBeTrue();
        }
    }

    [Fact]
    void should_preserve_every_template_import()
    {
        const string prefix = "Cratis.Stage.Rendering.Axon.Emitters.Runtime.Templates.";
        var assembly = typeof(AxonRuntimeEmitter).Assembly;
        foreach (var resource in assembly.GetManifestResourceNames().Where(name => name.StartsWith(prefix, StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(resource);
            using var reader = new StreamReader(stream);
            var imports = reader.ReadToEnd().Replace("__RUNTIME__", context.BasePackage + ".runtime", StringComparison.Ordinal)
                .Split('\n').Where(line => line.StartsWith("import ", StringComparison.Ordinal));
            var name = resource[(resource.LastIndexOf('.', resource.Length - ".java".Length - 1) + 1)..];
            var emitted = Template(name).Split('\n').Where(line => line.StartsWith("import ", StringComparison.Ordinal));
            emitted.SequenceEqual(imports.Order(StringComparer.Ordinal)).ShouldBeTrue();
        }
    }

    string Template(string name) => Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith($"/{name}", StringComparison.Ordinal)).Bytes.AsSpan());

    [Fact]
    void should_not_emit_a_custom_clock() =>
        context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("/Clock.java", StringComparison.Ordinal)).ShouldBeFalse();
}
