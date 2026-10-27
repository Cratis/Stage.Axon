// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonRendering;

public class when_reading_the_version : Specification
{
    string _version;

    void Because() => _version = AxonRendering.Version;

    [Fact] void should_use_the_package_version() => _version.ShouldEqual(typeof(AxonRendering).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0]);
    [Fact] void should_not_include_build_metadata() => _version.Contains('+').ShouldBeFalse();
    [Fact] void should_set_the_target_version() => AxonRendering.CreateProfile().TargetVersion.ShouldEqual(_version);
    [Fact] void should_set_the_renderer_version() => AxonRendering.CreateProfile().RendererVersion.ShouldEqual(_version);
}
