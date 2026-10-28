// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Runtime.for_AxonRuntimePackage;

public class when_resolving_runtime_types : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext _context;

    void Because() => _context = new AxonEmissionContext(request, new(request.Profile));

    [Fact] void should_qualify_a_generated_type_in_the_runtime_package() => AxonRuntimePackage.Type(_context, "UnsupportedImplementations").ShouldEqual(_context.BasePackage + ".runtime.UnsupportedImplementations");
    [Fact] void should_qualify_a_type_below_a_sub_package() => AxonRuntimePackage.Type(_context, "persistent.UniqueClaim").ShouldEqual("io.cratis.stage.axon.runtime.persistent.UniqueClaim");
    [Fact] void should_resolve_the_runtime_package() => AxonRuntimePackage.JavaPackage(_context).ShouldEqual(_context.BasePackage + ".runtime");
    [Fact] void should_resolve_the_inline_package_to_the_same_package() => AxonRuntimePackage.InlinePackage(_context).ShouldEqual(_context.BasePackage + ".runtime");
    [Fact] void should_resolve_a_module_type_to_the_module_package() => AxonRuntimePackage.Type("a.b", "EventSourceId").ShouldEqual("io.cratis.stage.axon.runtime.EventSourceId");
    [Fact] void should_resolve_a_generated_type_from_a_base_package() => AxonRuntimePackage.Type("a.b", "UnsupportedImplementations").ShouldEqual("a.b.runtime.UnsupportedImplementations");
    [Theory]
    [InlineData("ReadModelStore")]
    [InlineData("InMemoryReadModelStore")]
    [InlineData("ReadModelTransaction")]
    void should_resolve_the_read_model_helpers_to_the_module_package(string name) => AxonRuntimePackage.Type("a.b", name).ShouldEqual("io.cratis.stage.axon.runtime." + name);

    [Theory]
    [InlineData("persistent.UniqueClaim")]
    [InlineData("persistent.JpaUniqueValueStore")]
    [InlineData("persistent.PersistentConfiguration")]
    [InlineData("persistent.PersistentReadModelSafety")]
    void should_resolve_the_persistent_helpers_to_the_module_persistent_package(string name) => AxonRuntimePackage.Type("a.b", name).ShouldEqual("io.cratis.stage.axon.runtime." + name);

    [Fact] void should_pin_the_renderer_version() => AxonRuntimePackage.Version.ShouldEqual(AxonRendering.Version);
    [Fact] void should_name_thirty_module_types() => AxonRuntimePackage.ModuleTypes.Count.ShouldEqual(30);
    [Fact] void should_resolve_the_view_store_to_the_module() => AxonRuntimePackage.Type("a.b", "views.JpaReadModelStore").ShouldEqual("io.cratis.stage.axon.runtime.views.JpaReadModelStore");
    [Fact] void should_recognise_module_types_as_runtime_types() => AxonRuntimePackage.IsRuntimeType("a.b", "io.cratis.stage.axon.runtime.Caller").ShouldBeTrue();
    [Fact] void should_recognise_generated_runtime_types() => AxonRuntimePackage.IsRuntimeType("a.b", "a.b.runtime.UnsupportedImplementations").ShouldBeTrue();
    [Fact] void should_not_recognise_application_types_as_runtime_types() => AxonRuntimePackage.IsRuntimeType("a.b", "a.b.orders.X").ShouldBeFalse();
}
