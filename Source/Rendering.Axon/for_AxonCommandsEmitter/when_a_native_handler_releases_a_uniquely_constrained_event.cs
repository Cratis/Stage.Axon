// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter;

public class when_a_native_handler_releases_a_uniquely_constrained_event : when_a_native_handler_is_constrained
{
    protected override bool Releases => true;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void should_enforce_release_only_appends_in_a_private_body(bool strict) => AssertEnforced(strict);
}
