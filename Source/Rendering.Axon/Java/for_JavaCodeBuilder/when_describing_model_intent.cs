// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Java.for_JavaCodeBuilder;

public class when_describing_model_intent : Specification
{
    [Fact]
    void should_explain_an_unsupported_construct_with_its_model_path() =>
        new JavaCodeBuilder("example").UnsupportedIntent("capture invoice lines", "invoice.play/capture", "STAGE-AXON-CAPTURE")
            .Build().ShouldContain("// Screenplay wanted to capture invoice lines (invoice.play/capture), but this is not supported by the Axon target yet (STAGE-AXON-CAPTURE).\n");

    [Fact]
    void should_explain_the_difference_of_a_closest_equivalent() =>
        new JavaCodeBuilder("example").DiffersFromScreenplay("Only single-JVM atomicity is available.")
            .Build().ShouldContain("// Differs from Screenplay: Only single-JVM atomicity is available.\n");

    [Fact]
    void should_keep_model_text_inside_a_single_comment_line() =>
        new JavaCodeBuilder("example").DiffersFromScreenplay("a\r\nb\\u000a")
            .Build().ShouldContain("// Differs from Screenplay: a  b\\\\u000a\n");
}
