// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Constraints.for_AxonConstraintsEmitter;

public class when_a_constrained_event_shadows_adapter_types : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    [Theory]
    [InlineData("Throwable", "catch (java.lang.Throwable failure)")]
    [InlineData("Override", "@java.lang.Override")]
    [InlineData("Object", "java.lang.Object event")]
    [InlineData("List", "void append(java.util.List<?> events)")]
    [InlineData("EventAppender", "public static org.axonframework.messaging.eventhandling.gateway.EventAppender enforcing(")]
    [InlineData("ProcessingContext", "org.axonframework.messaging.core.unitofwork.ProcessingContext context")]
    [InlineData("Metadata", "org.axonframework.messaging.core.Metadata.emptyInstance()")]
    [InlineData("Message", "event instanceof org.axonframework.messaging.core.Message message")]
    [InlineData("CompletionException", "throw new java.util.concurrent.CompletionException(failure);")]
    [InlineData("ComponentDescriptor", "void describeTo(org.axonframework.common.infra.ComponentDescriptor descriptor)")]
    public void should_keep_the_event_short_and_qualify_the_framework_type(string name, string expected)
    {
        var contract = ScopeSelection.Select(request).SelectMany(slice => slice.Slice.Events).Single(contract => contract.Name == "OrderPlaced");
        var property = contract.Properties.Single(property => property.Name == "customer");
        var constraint = new SemanticConstraint("CustomerUnique", SemanticConstraintKind.UniquePropertyValue, SemanticConstraintScope.EventSequence, [new(contract.Id, [property.Id])], [], false, null);
        SemanticFeature Feature(SemanticFeature feature) => feature with
        {
            Features = [.. feature.Features.Select(Feature)],
            Slices = [.. feature.Slices.Select(slice => slice.Events.Any(candidate => candidate.Id == contract.Id)
                ? slice with { Events = [.. slice.Events.Select(candidate => candidate.Id == contract.Id ? candidate with { Name = name } : candidate)], Constraints = [constraint] }
                : slice)]
        };
        var application = request.Model.Application with { Modules = [.. request.Model.Application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })] };
        var model = ExecutableSemanticModel.Create(request.Model.LanguageVersion, request.Model.SemanticVersion, application);
        var context = new AxonEmissionContext(request with { Model = model }, new(request.Profile));
        new AxonConstraintsEmitter().Emit(context);
        var java = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/ModeledConstraints.java", StringComparison.Ordinal)).Bytes.AsSpan());
        java.ShouldContain("import sample.orders.events." + name + ";");
        java.ShouldContain("if (event instanceof " + name + " fact)");
        java.ShouldContain(expected);
    }
}
