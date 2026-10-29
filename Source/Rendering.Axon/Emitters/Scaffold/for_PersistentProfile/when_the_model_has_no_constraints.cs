// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Scaffold.for_PersistentProfile;

public class when_the_model_has_no_constraints : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    bool _offered;

    void Because() => _offered = PersistentProfile.Offered(new(request, new(request.Profile)));

    [Fact] void should_offer_the_profile() => _offered.ShouldBeTrue();
}
