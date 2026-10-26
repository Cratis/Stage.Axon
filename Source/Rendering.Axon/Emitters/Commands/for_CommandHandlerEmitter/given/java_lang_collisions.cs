// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands.for_CommandHandlerEmitter.given;

public class java_lang_collisions : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    protected SemanticCommand command;
    protected SemanticProperty response;
    private protected AxonEmissionContext context;
    LocatedSemanticSlice located;
    SemanticProperty template;

    protected void Prepare(SemanticPrimitiveType primitive, bool collection = false, bool collide = true)
    {
        located = ScopeSelection.Select(request).First(slice => !slice.Slice.Commands.IsEmpty);
        var seed = located.Slice.Commands[0];
        template = seed.Properties[^1];
        var contract = CommandAppends.Contract(new(request, new(request.Profile)), seed.Produces[0].EventContract);
        SemanticConcept[] concepts =
        [
            Concept("String", SemanticPrimitiveType.Text, collide), Concept("Long", SemanticPrimitiveType.WholeNumber, collide),
            Concept("Boolean", SemanticPrimitiveType.Boolean, collide), Concept("Object", SemanticPrimitiveType.Text, collide)
        ];
        var properties = concepts.Select(concept => Property("modeled" + concept.Name, SemanticTypeReference.ForConcept(concept.Id) with { IsOptional = true })).ToArray();
        var changed = contract with { Properties = [.. contract.Properties, .. properties] };
        var application = request.Model.Application with
        {
            Concepts = [.. request.Model.Application.Concepts, .. concepts],
            Modules = [.. request.Model.Application.Modules.Select(module => module with { Features = [.. module.Features.Select(feature => ChangeEvents(feature, changed))] })]
        };
        request = request with { Model = ExecutableSemanticModel.Create(request.Model.LanguageVersion, request.Model.SemanticVersion, application) };
        response = Property("response", SemanticTypeReference.ForPrimitive(primitive) with { IsCollection = collection });
        var produced = seed.Produces[0] with
        {
            Mappings = [.. seed.Produces[0].Mappings, .. properties.Select(property => seed.Produces[0].Mappings[0] with
            {
                TargetProperty = property.Id,
                Source = new SemanticValueExpression(Value(concepts.Single(concept => concept.Id == property.Type.Target).Primitive))
            })]
        };
        command = seed with
        {
            Properties = [.. seed.Properties, response, Property("count", SemanticTypeReference.ForPrimitive(SemanticPrimitiveType.WholeNumber)),
                Property("text", SemanticTypeReference.ForPrimitive(SemanticPrimitiveType.Text)), Property("flag", SemanticTypeReference.ForPrimitive(SemanticPrimitiveType.Boolean)),
                Property("modeledCount", SemanticTypeReference.ForConcept(concepts[1].Id))],
            Response = new SemanticScalarCommandResponse(response.Id, response.Type),
            Produces = [produced]
        };
    }

    protected SemanticCondition Condition(SemanticComparisonOperator comparison)
    {
        var numeric = Compare("count", comparison, new SemanticNumberValue(1m));
        var text = Compare("text", SemanticComparisonOperator.Equal, SemanticValue.Text("java.lang.String"));
        var boolean = Compare("flag", SemanticComparisonOperator.NotEqual, new SemanticBooleanValue(false));
        return new SemanticLogicalCondition(numeric, SemanticLogicalOperator.And, new SemanticLogicalCondition(text, SemanticLogicalOperator.Or, boolean));
    }

    protected string Render()
    {
        context = new(request, new(request.Profile));
        var source = CommandSource.Create(context, command, CommandAppends.Contract(context, command.Produces[0].EventContract));
        new CommandHandlerEmitter(context, located, command, source).Emit();
        return Text("PlaceOrderHandler.java");
    }

    protected string Text(string suffix) => Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith(suffix, StringComparison.Ordinal)).Bytes.AsSpan());

    static SemanticConcept Concept(string name, SemanticPrimitiveType type, bool collide) => new(SemanticId.Create(SemanticKind.Concept, "collision" + name), collide ? name : "Modeled" + name, type, [], []);

    static SemanticFeature ChangeEvents(SemanticFeature feature, SemanticEventContract changed) => feature with
    {
        Features = [.. feature.Features.Select(child => ChangeEvents(child, changed))],
        Slices = [.. feature.Slices.Select(slice => slice with { Events = [.. slice.Events.Select(candidate => candidate.Id == changed.Id ? changed : candidate)], Specifications = [] })]
    };

    SemanticProperty Property(string name, SemanticTypeReference type) => template with
    {
        Id = SemanticId.Create(SemanticKind.Property, name),
        Name = name,
        Type = type,
        IsIdentifier = false,
        IsGenerated = false
    };

    SemanticComparison Compare(string name, SemanticComparisonOperator operation, SemanticValue value) =>
        new(new(command.Properties.Single(property => property.Name == name).Id, null), operation, new(default, value));

    static SemanticValue Value(SemanticPrimitiveType primitive) => primitive switch
    {
        SemanticPrimitiveType.WholeNumber => new SemanticNumberValue(1m),
        SemanticPrimitiveType.Boolean => new SemanticBooleanValue(true),
        _ => SemanticValue.Text("literal")
    };
}
