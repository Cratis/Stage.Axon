// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views.for_NativeViewSpecifications;

public class when_a_model_claims_the_list_name : given.a_native_view_request
{
    void Establish()
    {
        var application = _request.Model.Application;
        static SemanticFeature Rename(SemanticFeature feature) => feature with
        {
            Features = [.. feature.Features.Select(Rename)],
            Slices = [.. feature.Slices.Select(slice => slice with
            {
                ReadModels = [.. slice.ReadModels.Select(model => model.Name == "Categories" ? model with { Name = "List" } : model)]
            })]
        };
        _request = _request with
        {
            Model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application with
            {
                Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(Rename)] })]
            })
        };
        _context = new(_request, new(_request.Profile));
    }

    void Because()
    {
        new AxonViewSpecsEmitter().Emit(_context);
        _java = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/ListViewSpecificationsTest.java", StringComparison.Ordinal)));
    }

    [Fact] void should_keep_the_model_short() => _java.ShouldContain("ListRepository repository");
    [Fact] void should_qualify_the_colliding_framework_list() => _java.ShouldContain("java.util.List.of(");
    [Fact] void should_not_import_the_colliding_framework_list() => _java.ShouldNotContain("import java.util.List;");
    [Fact] void should_still_shorten_the_security_holder() => _java.ShouldContain("import org.springframework.security.core.context.SecurityContextHolder;");
}
