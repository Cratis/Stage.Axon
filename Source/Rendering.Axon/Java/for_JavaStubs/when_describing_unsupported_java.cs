// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Java.for_JavaStubs;

public class when_describing_unsupported_java : Specification
{
    [Fact]
    void should_emit_a_compiling_marker_interface()
    {
        var source = JavaStubs.Interface("example.runtime", "missing-performer");
        Assert.Contains("package example.runtime;", source);
        Assert.Contains("public interface MissingPerformer {\n}", source);
    }

    [Fact]
    void should_escape_the_reason_and_fail_loudly() =>
        JavaStubs.ThrowingBody("STAGE-AXON-IMPL", "Missing \"body\"\nnext").ShouldEqual("{\n    throw new UnsupportedOperationException(\"STAGE-AXON-IMPL: Missing \\\"body\\\"\\nnext\");\n}");
}
