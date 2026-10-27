// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_ProjectionHandlerBinding;

public class when_pre_reserving_projection_keys
{
    [Fact] void should_reserve_every_key_of_a_multi_key_handler() => ProjectionHandlerBinding.PreReserved(["\"global\"", "event.id()"]).ShouldContainOnly("\"global\"", "event.id()");
    [Fact] void should_not_reserve_a_single_key() => ProjectionHandlerBinding.PreReserved(["\"global\""]).ShouldBeEmpty();
    [Fact] void should_leave_an_unemittable_key_to_degrade_instead_of_failing_the_render() => ProjectionHandlerBinding.PreReserved(["\"global\"", null]).ShouldBeEmpty();
}
