// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Verification.for_CorpusApplications;

/// <summary>Screenplay 6 requires a screen action and form for every state-changing command the UI issues; the sample-navigation model keeps its former "New order" toolbar flow by exposing PlaceOrder through a NewOrder screen and form.</summary>
public class when_preserving_the_new_order_flow : Specification
{
    string _model;

    void Establish() => _model = File.ReadAllText(Path.Combine(given.the_repository.Root(), "Corpus/live-navigation/model/application.play"));

    [Fact] void should_keep_the_new_order_toolbar_button() => _model.ShouldContain("button new title \"New order\" slice Sales.Orders.PlaceOrder");
    [Fact] void should_expose_place_order_through_a_screen_action() => _model.ShouldContain("screen NewOrder\n        title \"New order\"\n        action PlaceOrder");
    [Fact] void should_declare_the_place_order_form() => _model.ShouldContain("form PlaceOrderForm for PlaceOrder");
}
