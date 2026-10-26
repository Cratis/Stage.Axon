// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Runtime.for_AxonRuntimeEmitter;

public class when_emitting_the_persistent_probe_template : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string _source;

    void Because()
    {
        var context = new AxonEmissionContext(request, new(request.Profile));
        new AxonRuntimeEmitter().Emit(context);
        _source = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/PersistentProbeSupport.java", StringComparison.Ordinal)).Bytes.AsSpan());
    }

    [Fact] void should_import_the_candidate_scanner() => _source.ShouldContain("import org.springframework.context.annotation.ClassPathScanningCandidateComponentProvider;");
    [Fact] void should_import_the_configuration_annotation() => _source.ShouldContain("import org.springframework.context.annotation.Configuration;");
    [Fact] void should_import_the_type_filter() => _source.ShouldContain("import org.springframework.core.type.filter.AnnotationTypeFilter;");
    [Fact] void should_import_arrays() => _source.ShouldContain("import java.util.Arrays;");
    [Fact] void should_use_short_names_in_the_scan() => _source.ShouldContain("scanner.addIncludeFilter(new AnnotationTypeFilter(Configuration.class));");
    [Fact] void should_not_qualify_the_scanner() => _source.ShouldNotContain("new org.springframework.context.annotation.ClassPathScanningCandidateComponentProvider");
    [Fact] void should_not_qualify_arrays() => _source.ShouldNotContain("java.util.Arrays.stream");
}
