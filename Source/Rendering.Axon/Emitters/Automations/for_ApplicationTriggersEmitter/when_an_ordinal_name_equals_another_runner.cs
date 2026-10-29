// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Automations.for_ApplicationTriggersEmitter;

public class when_an_ordinal_name_equals_another_runner : Specification
{
    string[] _names;

    void Because() => _names = ApplicationTriggersEmitter.Disambiguate(["runTotal", "runTotal", "runTotal2", "runOther"]);

    [Fact] void should_keep_the_unique_names() => _names[2].ShouldEqual("runTotal2");
    [Fact] void should_keep_the_other_unique_name() => _names[3].ShouldEqual("runOther");
    [Fact] void should_give_every_runner_a_distinct_name() => _names.Distinct(StringComparer.Ordinal).Count().ShouldEqual(_names.Length);
    [Fact] void should_move_past_the_taken_ordinal() => _names[1].ShouldEqual("runTotal3");
}
