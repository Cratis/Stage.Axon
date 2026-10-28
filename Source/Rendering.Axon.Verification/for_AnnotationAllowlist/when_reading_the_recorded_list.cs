// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;

namespace Cratis.Stage.Rendering.Axon.Verification.for_AnnotationAllowlist;

public class when_reading_the_recorded_list
{
    [Fact]
    public void should_record_only_axon_annotations_and_include_the_known_handlers()
    {
        var recorded = AnnotationAllowlist.Load(for_CorpusApplications.given.the_repository.Root());
        Assert.NotEmpty(recorded);
        Assert.All(recorded, name => Assert.StartsWith("org.axonframework.", name, StringComparison.Ordinal));
        string[] expected =
        [
            "org.axonframework.messaging.commandhandling.annotation.CommandHandler",
            "org.axonframework.messaging.commandhandling.annotation.Command",
            "org.axonframework.eventsourcing.annotation.EventSourcedEntity",
            "org.axonframework.extension.spring.stereotype.EventSourced",
            "org.axonframework.modelling.annotation.InjectEntity",
            "org.axonframework.messaging.queryhandling.annotation.QueryHandler"
        ];
        Assert.All(expected, name => Assert.Contains(name, recorded));
    }
}
