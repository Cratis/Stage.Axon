// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Verification.for_CorpusRenderer;

public class when_choosing_the_bearer_profile : Specification
{
    [Fact] void should_request_bearer_tokens() => Encoding.UTF8.GetString(CorpusRenderer.BearerProfile.Inputs.Single(input => input.Name == "http-authentication").Bytes.AsSpan()).ShouldEqual("bearer");
    [Fact] void should_keep_the_typescript_client() => CorpusRenderer.BearerProfile.Inputs.Count(input => input.Name == "typescript-client").ShouldEqual(1);
    [Fact] void should_keep_the_other_apps_on_basic() => CorpusRenderer.Profile.Inputs.Any(input => input.Name == "http-authentication").ShouldBeFalse();
}
