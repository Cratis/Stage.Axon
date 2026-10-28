// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;

namespace Cratis.Stage.Rendering.Axon.Admission.for_AxonDegradation;

public class when_recording_a_summary_note
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void should_keep_distinct_notes_without_recording_degradations(bool strict)
    {
        var degradation = new AxonDegradation(AxonRendering.CreateProfile(strict: strict));
        degradation.Note("x");
        degradation.Note("x");
        degradation.Note("y");
        degradation.Note("");
        degradation.Note(" ");
        Assert.Equal(["x", "y"], degradation.Notes);
        Assert.Empty(degradation.UnsupportedConstructs);
        Assert.Empty(degradation.Intents);
        Assert.Empty(degradation.Warnings);
        Assert.Empty(degradation.Refusals);
    }
}
