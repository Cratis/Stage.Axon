// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Verification.for_CorpusRenderer;

public class when_choosing_the_corpus_profile : Specification
{
    [Fact] void should_request_the_typescript_client() => CorpusRenderer.Profile.Inputs.Count(input => input.Name == "typescript-client").ShouldEqual(1);
    [Fact] void should_keep_the_default_options() => CorpusRenderer.Profile.Inputs.Any(input => input.Name == "axon-options").ShouldBeTrue();
}
