// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter;

public class when_the_read_model_is_named_result : given.a_query_collision
{
    void Because()
    {
        _model = _model with { Name = "Result" };
        Render();
    }

    [Fact] void should_qualify_the_model_inside_the_nested_result() => Code("FindTotal").ShouldContain($"public record Result(List<{_package}.Result> rows)");
    [Fact] void should_not_import_a_type_shadowed_by_the_nested_result() => Code("FindTotal").ShouldNotContain($"import {_package}.Result;");
    [Fact] void should_use_the_same_package_model_in_the_handler() => Code("FindTotalHandler").ShouldContain("ReadModelStore<Object, Result> store;");
    [Fact] void should_not_import_the_handlers_own_package() => Code("FindTotalHandler").ShouldNotContain($"import {_package}.Result;");
}
