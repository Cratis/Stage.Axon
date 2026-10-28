// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Client.for_AxonClientEmitter;

public class when_a_request_member_has_a_different_java_name : given.a_client_request
{
    string module;

    void Because()
    {
        Render(application => ChangeSlices(application, slice => slice with
        {
            Commands = [.. slice.Commands.Select(command => command with
            {
                Properties = [.. command.Properties.Select((property, index) => property with { Name = index switch { 0 => "class", 1 => "display-name", _ => property.Name } })]
            })]
        }));
        module = Text("client/src/orders/orders/placeorder.ts");
    }

    [Fact] void should_use_the_modelled_keyword() => module.ShouldContain("readonly class:");
    [Fact] void should_quote_a_non_identifier() => module.ShouldContain("readonly \"display-name\":");
    [Fact] void should_not_use_the_java_keyword_escape() => module.ShouldNotContain("class_:");
    [Fact] void should_not_use_the_normalized_java_name() => module.ShouldNotContain("displayName:");
}
