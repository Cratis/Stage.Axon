// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views.for_NativeViewSpecifications;

public class when_emitting_caller_security_fixtures : given.a_native_view_request
{
    void Establish() => _context = new(_request, new(_request.Profile));

    void Because()
    {
        new AxonViewSpecsEmitter().Emit(_context);
        _java = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/CategoriesViewSpecificationsTest.java", StringComparison.Ordinal)));
    }

    [Fact] void should_import_the_context_holder() => _java.ShouldContain("import org.springframework.security.core.context.SecurityContextHolder;");
    [Fact] void should_import_the_authenticated_token() => _java.ShouldContain("import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;");
    [Fact] void should_import_the_authority() => _java.ShouldContain("import org.springframework.security.core.authority.SimpleGrantedAuthority;");
    [Fact] void should_import_list() => _java.ShouldContain("import java.util.List;");
    [Fact] void should_use_the_short_context_holder() => _java.ShouldContain("SecurityContextHolder.setContext(SecurityContextHolder.createEmptyContext());");
    [Fact] void should_not_qualify_the_context_holder() => _java.ShouldNotContain("org.springframework.security.core.context.SecurityContextHolder.");
    [Fact] void should_not_qualify_the_authority() => _java.ShouldNotContain("new org.springframework.security.core.authority.SimpleGrantedAuthority");
    [Fact] void should_not_qualify_list() => _java.ShouldNotContain("java.util.List.of(");
    [Fact] void should_keep_the_role_literal() => _java.ShouldContain("new SimpleGrantedAuthority(\"ROLE_CatalogManager\")");
}
