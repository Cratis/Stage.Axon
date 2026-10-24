// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Conformance.for_JUnitXmlResults;

public class when_reading_native_results : Specification
{
    string directory;

    void Establish()
    {
        directory = Path.Combine(for_RenderCoverage.given.a_render_request.Root(), ".ai-work", "770", "conformance-specs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
    }

    [Fact]
    void should_correlate_manifest_methods_and_count_skips_as_nonpasses()
    {
        File.WriteAllText(Path.Combine(directory, "stage-specifications.txt"), "sem1:passed\texample.Tests#works\nsem1:skip\tdisabled KNOWN\texample.Tests#skip\nsem1:failed\texample.Tests#fails\nsem1:error\texample.Tests#error\n");
        File.WriteAllText(Path.Combine(directory, "TEST.xml"), "<testsuite><testcase classname='example.Tests' name='works'/><testcase classname='example.Tests' name='skip'><skipped/></testcase><testcase classname='example.Tests' name='fails'><failure/></testcase><testcase classname='example.Tests' name='error'><error/></testcase><testcase name='helper'/></testsuite>");
        var entries = StageSpecificationManifest.Read(Path.Combine(directory, "stage-specifications.txt"));
        var results = JUnitXmlResults.ReadDirectory(directory);
        entries.Length.ShouldEqual(4);
        entries[0].DisabledCode.ShouldBeNull();
        entries[1].DisabledCode.ShouldEqual("KNOWN");
        results.Length.ShouldEqual(4);
        results.Single(result => result.SemanticId == "sem1:passed").Passed.ShouldBeTrue();
        results.Count(result => !result.Passed).ShouldEqual(3);
    }

    [Fact]
    void should_correlate_semantic_display_names_and_keep_duplicates()
    {
        File.WriteAllText(Path.Combine(directory, "TEST.xml"), "<testsuites><testsuite><testcase name='sem1:direct'/><testcase name='method'><properties><property name='display-name' value='sem1:display'/></properties></testcase><testcase name='sem1:direct'/></testsuite></testsuites>");
        JUnitXmlResults.ReadDirectory(directory).Count(result => result.SemanticId == "sem1:direct").ShouldEqual(2);
        JUnitXmlResults.ReadDirectory(directory).ShouldContain(result => result.SemanticId == "sem1:display" && result.Passed);
    }

    [Fact]
    void should_reject_a_dtd_instead_of_resolving_entities()
    {
        File.WriteAllText(Path.Combine(directory, "TEST.xml"), "<!DOCTYPE testsuite [<!ENTITY value 'sem1:expanded'>]><testsuite><testcase name='&value;'/></testsuite>");
        Assert.Throws<FormatException>(() => JUnitXmlResults.ReadDirectory(directory)).Message.ShouldContain(ConformanceCodes.MalformedManifest);
    }

    [Fact]
    void should_reject_malformed_xml()
    {
        File.WriteAllText(Path.Combine(directory, "TEST.xml"), "<testsuite>");
        Assert.Throws<FormatException>(() => JUnitXmlResults.ReadDirectory(directory)).Message.ShouldContain(ConformanceCodes.MalformedManifest);
    }

    [Fact]
    void should_reject_malformed_manifest_entries()
    {
        var path = Path.Combine(directory, "stage-specifications.txt");
        File.WriteAllText(path, "sem1:bad\tdisabled \texample.Tests#test\n");
        Assert.Throws<FormatException>(() => StageSpecificationManifest.Read(path)).Message.ShouldContain(ConformanceCodes.MalformedManifest);
    }
}
