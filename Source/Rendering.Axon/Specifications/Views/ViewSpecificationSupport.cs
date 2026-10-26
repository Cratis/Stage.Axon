// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views;

internal static class ViewSpecificationSupport
{
    internal static bool Legacy(SemanticSpecification spec, SemanticReadModel model, ViewJavaTypes types) =>
        Given(spec) && NoWhen(spec) && Then(spec) && SingleModel(spec) &&
        spec.ThenAbsentReadModels.All(expected => ViewExpectedKeys.Expression(expected.Key, model, types) is not null);

    static bool Given(SemanticSpecification spec) => spec.GivenReadModels.Length == 0 && spec.GivenClock is null && spec.GivenCaller is null;

    static bool NoWhen(SemanticSpecification spec) => NoDelivery(spec) && NoEnvironment(spec);

    static bool NoDelivery(SemanticSpecification spec) => spec.When is null && spec.WhenAppended is null && spec.WhenRedelivered is null;

    static bool NoEnvironment(SemanticSpecification spec) => spec.WhenClock is null && spec.WhenCapture is null && spec.WhenTrigger is null;

    static bool Then(SemanticSpecification spec) => NoEffects(spec) && spec.ThenReadModels.All(expected => !expected.Exactly);

    static bool NoEffects(SemanticSpecification spec) => !spec.ThenDenied && spec.ThenErrors.Length == 0 && spec.ThenEvents.Length == 0 && spec.ThenQueries.Length == 0;

    static bool SingleModel(SemanticSpecification spec) => spec.ThenReadModels.Select(expected => expected.ReadModel)
        .Concat(spec.ThenAbsentReadModels.Select(expected => expected.ReadModel)).Distinct().Count() == 1;
}
