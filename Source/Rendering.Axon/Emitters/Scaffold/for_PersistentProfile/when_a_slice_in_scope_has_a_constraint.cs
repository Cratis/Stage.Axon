// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Scaffold.for_PersistentProfile;

public class when_a_slice_in_scope_has_a_constraint : given.a_constrained_orders_request
{
    bool _offered;

    void Because() => _offered = PersistentProfile.Offered(new(constrained, new(constrained.Profile)));

    [Fact] void should_not_offer_the_profile() => _offered.ShouldBeFalse();
}
