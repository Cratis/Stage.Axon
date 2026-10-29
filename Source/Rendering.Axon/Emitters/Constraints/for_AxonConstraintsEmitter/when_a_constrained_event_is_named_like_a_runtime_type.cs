// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Constraints.for_AxonConstraintsEmitter;

public class when_a_constrained_event_is_named_like_a_runtime_type : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string _java;

    void Because()
    {
        var contract = ScopeSelection.Select(request).SelectMany(slice => slice.Slice.Events).Single(contract => contract.Name == "OrderPlaced");
        var property = contract.Properties.Single(property => property.Name == "customer");
        var constraint = new SemanticConstraint("CustomerUnique", SemanticConstraintKind.UniquePropertyValue, SemanticConstraintScope.EventSequence, [new(contract.Id, [property.Id])], [], false, null);
        SemanticFeature Feature(SemanticFeature feature) => feature with
        {
            Features = [.. feature.Features.Select(Feature)],
            Slices = [.. feature.Slices.Select(slice => slice.Events.Any(candidate => candidate.Id == contract.Id)
                ? slice with { Events = [.. slice.Events.Select(candidate => candidate.Id == contract.Id ? candidate with { Name = "UniqueValueStore" } : candidate)], Constraints = [constraint] }
                : slice)]
        };
        var application = loaded.Model.Application with { Modules = [.. loaded.Model.Application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })] };
        var model = ExecutableSemanticModel.Create(loaded.Model.LanguageVersion, loaded.Model.SemanticVersion, application);
        var context = new AxonEmissionContext(request with { Model = model }, new(request.Profile));
        new AxonConstraintsEmitter().Emit(context);
        _java = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/CustomerUniqueSetValidation.java", StringComparison.Ordinal)).Bytes.AsSpan());
    }

    [Fact] void should_import_the_constrained_application_event() => _java.ShouldContain("import sample.orders.events.UniqueValueStore;");
    [Fact] void should_keep_the_event_short() => _java.ShouldContain("public void on(UniqueValueStore event)");
    [Fact] void should_not_reference_a_runtime_store() => _java.ShouldNotContain("io.cratis.stage.axon.runtime");
    [Fact] void should_name_the_entity_after_the_constraint() => _java.ShouldContain("public final class CustomerUniqueSetValidation");
}
