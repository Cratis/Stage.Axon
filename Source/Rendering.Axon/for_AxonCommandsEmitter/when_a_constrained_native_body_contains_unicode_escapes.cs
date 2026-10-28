// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter;

public class when_a_constrained_native_body_contains_unicode_escapes : when_a_native_handler_is_constrained
{
    protected override string CreateBody(string append) => base.CreateBody(append) + "\n" + @"Event\u0041ppender.for\u0043ontext(context).append(" + append + ");";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void should_fail_closed_without_consuming_the_body(bool strict) => AssertRefused(strict, "enforce modeled uniqueness for every event a native command body appends");
}
