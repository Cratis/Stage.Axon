// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Admission.for_HttpAuthenticationInput;

public class when_requesting_bearer_tokens : Specification
{
    ArtifactRenderProfile original;
    ArtifactRenderProfile once;
    ArtifactRenderProfile twice;

    void Because()
    {
        original = AxonRendering.WithTypeScriptClient(AxonRendering.CreateProfile(strict: true));
        once = AxonRendering.WithBearerTokens(original);
        twice = AxonRendering.WithBearerTokens(once);
    }

    [Fact] void should_add_exactly_one_input() => once.Inputs.Count(input => input.Name == HttpAuthenticationInput.Name).ShouldEqual(1);
    [Fact] void should_use_version_one() => once.Inputs.Single(input => input.Name == HttpAuthenticationInput.Name).Version.ShouldEqual("1");
    [Fact] void should_carry_bearer() => Encoding.UTF8.GetString(once.Inputs.Single(input => input.Name == HttpAuthenticationInput.Name).Bytes.AsSpan()).ShouldEqual("bearer");
    [Fact] void should_request_bearer() => HttpAuthenticationInput.Bearer(once).ShouldBeTrue();
    [Fact] void should_keep_other_inputs_in_order() => once.Inputs.Where(input => input.Name != HttpAuthenticationInput.Name).Select(input => input.Name).ShouldEqual(original.Inputs.Select(input => input.Name));
    [Fact] void should_be_idempotent() => twice.Inputs.Select(input => (input.Name, input.Version, Encoding.UTF8.GetString(input.Bytes.AsSpan()))).ShouldEqual(once.Inputs.Select(input => (input.Name, input.Version, Encoding.UTF8.GetString(input.Bytes.AsSpan()))));
}
