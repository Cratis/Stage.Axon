// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Runtime.for_AxonRuntimePackage;

public class when_resolving_runtime_types : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext _context;

    void Because() => _context = new AxonEmissionContext(request, new(request.Profile));

    [Fact] void should_qualify_a_type_in_the_runtime_package() => AxonRuntimePackage.Type(_context, "ReadModelStore").ShouldEqual(_context.BasePackage + ".runtime.ReadModelStore");
    [Fact] void should_qualify_a_type_below_a_sub_package() => AxonRuntimePackage.Type(_context, "persistent.UniqueClaim").ShouldEqual(_context.BasePackage + ".runtime.persistent.UniqueClaim");
    [Fact] void should_resolve_the_runtime_package() => AxonRuntimePackage.JavaPackage(_context).ShouldEqual(_context.BasePackage + ".runtime");
    [Fact] void should_resolve_the_inline_package_to_the_same_package() => AxonRuntimePackage.InlinePackage(_context).ShouldEqual(_context.BasePackage + ".runtime");
    [Fact] void should_resolve_from_a_base_package() => AxonRuntimePackage.Type("a.b", "EventSourceId").ShouldEqual("a.b.runtime.EventSourceId");
}
