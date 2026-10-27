// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Client.for_TypeScriptClientInput;

public class when_requesting_the_client : Specification
{
    ArtifactRenderProfile original;
    ArtifactRenderProfile once;
    ArtifactRenderProfile twice;

    void Because()
    {
        original = AxonRendering.AllowApproximations(AxonRendering.CreateProfile(strict: true), "STAGE-AXON-0507");
        once = AxonRendering.WithTypeScriptClient(original);
        twice = AxonRendering.WithTypeScriptClient(once);
    }

    [Fact] void should_add_exactly_one_input() => once.Inputs.Count(input => input.Name == Admission.TypeScriptClientInput.Name).ShouldEqual(1);
    [Fact] void should_use_version_one() => once.Inputs.Single(input => input.Name == Admission.TypeScriptClientInput.Name).Version.ShouldEqual("1");
    [Fact] void should_carry_true() => Encoding.UTF8.GetString(once.Inputs.Single(input => input.Name == Admission.TypeScriptClientInput.Name).Bytes.AsSpan()).ShouldEqual("true");
    [Fact] void should_keep_the_other_inputs_in_order() => once.Inputs.Where(input => input.Name != Admission.TypeScriptClientInput.Name).Select(input => input.Name).ShouldEqual(original.Inputs.Select(input => input.Name));
    [Fact] void should_be_idempotent() => twice.Inputs.Select(input => (input.Name, input.Version, Encoding.UTF8.GetString(input.Bytes.AsSpan()))).ShouldEqual(once.Inputs.Select(input => (input.Name, input.Version, Encoding.UTF8.GetString(input.Bytes.AsSpan()))));
}
