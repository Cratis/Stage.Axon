// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Constraints.for_AxonConstraintsEmitter;

public class when_constrained_events_in_different_modules_have_the_same_name : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string _java;

    void Because()
    {
        var slices = ScopeSelection.Select(request).ToArray();
        var target = slices.SelectMany(slice => slice.Slice.Events).Single(contract => contract.Name == "OrderPlaced");
        var released = slices.Single(slice => slice.Slice.Events.Any(contract => contract.Name == "OrderMoved"));
        var release = released.Slice.Events.Single(contract => contract.Name == "OrderMoved");
        var constraint = new SemanticConstraint("CustomerUnique", SemanticConstraintKind.UniquePropertyValue, SemanticConstraintScope.EventSequence, [new(target.Id, [target.Properties.Single(property => property.Name == "customer").Id])], [release.Id], false, null);
        SemanticFeature Feature(SemanticFeature feature) => feature with
        {
            Features = [.. feature.Features.Select(Feature)],
            Slices = [.. feature.Slices.Where(slice => slice.Id != released.Slice.Id).Select(slice => slice with
            {
                Events = [.. slice.Events.Select(contract => contract.Id == target.Id ? contract with { Name = "Changed" } : contract)],
                Constraints = slice.Events.Any(contract => contract.Id == target.Id) ? [constraint] : []
            })]
        };
        var application = request.Model.Application;
        var other = released.Module with
        {
            Id = SemanticId.Create(SemanticKind.Module, "Shipping"),
            Name = "Shipping",
            Features = [released.Features[^1] with { Id = SemanticId.Create(SemanticKind.Feature, "Shipping.Events"), Features = [], Slices = [released.Slice with { Events = [release with { Name = "Changed" }], Constraints = [] }] }]
        };
        var model = ExecutableSemanticModel.Create(request.Model.LanguageVersion, request.Model.SemanticVersion, application with { Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] }), other] });
        var context = new AxonEmissionContext(request with { Model = model }, new(request.Profile));
        new AxonConstraintsEmitter().Emit(context);
        _java = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/ModeledConstraints.java", StringComparison.Ordinal)).Bytes.AsSpan());
    }

    [Fact] void should_import_the_first_event() => _java.ShouldContain("import sample.orders.events.Changed;");
    [Fact] void should_use_the_first_event_for_assignment() => _java.ShouldContain("if (event instanceof Changed fact) store.assign(");
    [Fact] void should_qualify_the_second_event_for_release() => _java.ShouldContain("if (event instanceof sample.shipping.events.Changed fact) store.releaseOwner(");
    [Fact] void should_not_import_the_second_event() => _java.ShouldNotContain("import sample.shipping.events.Changed;");
}
