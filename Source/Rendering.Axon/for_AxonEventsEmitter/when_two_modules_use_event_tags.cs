// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Events;
using Cratis.Stage.Rendering.Axon.Java;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonEventsEmitter;

public class when_two_modules_use_event_tags : Specification
{
    string first;
    string second;
    string code;

    void Because()
    {
        var builder = new JavaCodeBuilder("sample.commands");
        first = EventTagsEmitter.Reference("sample.orders.events.EventTags.ORDER_ID", builder, "sample.commands");
        second = EventTagsEmitter.Reference("sample.shipping.events.EventTags.SHIPMENT_ID", builder, "sample.commands");
        code = builder.Build();
    }

    [Fact] void should_import_the_first_tag_owner() => code.ShouldContain("import sample.orders.events.EventTags;");
    [Fact] void should_keep_the_first_tag_reference_short() => first.ShouldEqual("EventTags.ORDER_ID");
    [Fact] void should_qualify_the_colliding_tag_owner() => second.ShouldEqual("sample.shipping.events.EventTags.SHIPMENT_ID");
    [Fact] void should_not_import_the_colliding_tag_owner() => code.ShouldNotContain("import sample.shipping.events.EventTags;");
    [Fact] void should_never_import_a_constant_as_a_type() => code.ShouldNotContain("import sample.orders.events.EventTags.ORDER_ID;");
}
