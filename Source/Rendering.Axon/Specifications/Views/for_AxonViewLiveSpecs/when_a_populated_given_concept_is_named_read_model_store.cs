// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views.for_AxonViewLiveSpecs;

public class when_a_populated_given_concept_is_named_read_model_store : for_AxonViewSpecsEmitter.given.a_populated_concept_collision
{
    string _subscription;
    string _sse;

    void Because()
    {
        Prepare(true);
        var query = _located.Slice.Queries.Single(query => query.Name == "AllCategories") with { Authorization = null };
        var model = _located.Slice.ReadModels.Single();
        AxonViewLiveSpecs.Emit(_context, new ViewJavaTypes(_request.Model.Application), _located, query, _located, model);
        _subscription = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/AllCategoriesSubscriptionTest.java", StringComparison.Ordinal)));
        _sse = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/AllCategoriesSseTest.java", StringComparison.Ordinal)));
    }

    [Fact] void should_import_the_subscription_concept() => _subscription.ShouldContain("import nativeviews.concepts.ReadModelStore;");
    [Fact] void should_construct_the_populated_subscription_concept() => _subscription.ShouldContain("new ReadModelStore(\"visible\")");
    [Fact] void should_qualify_the_subscription_store() => _subscription.ShouldContain("nativeviews.runtime.ReadModelStore<Object, Categories> store");
    [Fact] void should_not_import_the_subscription_store() => _subscription.ShouldNotContain("import nativeviews.runtime.ReadModelStore;");
    [Fact] void should_import_the_sse_concept() => _sse.ShouldContain("import nativeviews.concepts.ReadModelStore;");
    [Fact] void should_construct_the_populated_sse_concept() => _sse.ShouldContain("new ReadModelStore(\"visible\")");
    [Fact] void should_qualify_the_sse_store() => _sse.ShouldContain(", nativeviews.runtime.ReadModelStore.class);");
    [Fact] void should_not_import_the_sse_store() => _sse.ShouldNotContain("import nativeviews.runtime.ReadModelStore;");
}
