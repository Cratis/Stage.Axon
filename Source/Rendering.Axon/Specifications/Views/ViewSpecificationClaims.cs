// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Events;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Runtime;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views;

internal sealed class ViewSpecificationClaims(AxonEmissionContext context, ViewJavaTypes types)
{
    readonly ViewConceptValues _concepts = new(context.Request.Model.Application);

    internal void Specification(SemanticSpecification spec, SemanticReadModel model, JavaCodeBuilder code)
    {
        var application = context.Request.Model.Application;
        var owner = ScopeSelection.Locate(application).Single(slice => slice.Slice.ReadModels.Any(candidate => candidate.Id == model.Id));
        var projection = owner.Slice.Projections.Single(candidate => candidate.ReadModel == model.Id);
        var root = ProjectionScopeNode.Create(application, types, projection, model, JavaNames.Type(model.Name));
        foreach (var given in spec.GivenEvents.Where(given => Handles(root, given.EventContract))) Event(given, code, spec.GivenCaller is not null);
        Appended(spec.WhenAppended, code);
        foreach (var expected in spec.ThenReadModels.Where(expected => expected.ReadModel == model.Id))
        {
            foreach (var value in expected.Values) Literal(value.Value, model.Properties.First(property => property.Id == value.TargetProperty).Type, code);
            Key(expected.Key, model, code);
        }
        foreach (var absent in spec.ThenAbsentReadModels.Where(expected => expected.ReadModel == model.Id)) Key(absent.Key, model, code);
    }

    internal void Event(SemanticSpecificationEvent given, JavaCodeBuilder code, bool normalized = false)
    {
        var contract = ScopeSelection.Locate(context.Request.Model.Application).SelectMany(slice => slice.Slice.Events).Single(candidate => candidate.Id == given.EventContract);
        var primary = AxonEventSources.PrimaryTag(context, contract);
        types.EventType(given.EventContract, code);
        foreach (var property in contract.Properties) Literal(Value(given, property, primary.ComponentName, normalized), property.Type, code);
        if (contract.Properties.Any(property => JavaNames.Member(property.Name) == primary.ComponentName)) return;
        if (!AxonRuntimePackage.IsRuntimeType(types.BasePackage, primary.JavaType)) code.Reference(primary.JavaType);
        if (given.EventSource is { } source && primary.JavaType != AxonEventSources.FallbackType(types.BasePackage)) Literal(source.Value, source.Type, code);
    }

    static SemanticValue? Value(SemanticSpecificationEvent given, SemanticProperty property, string primary, bool normalized)
    {
        if (normalized && JavaNames.Member(property.Name) == primary) return given.EventSource?.Value;
        return given.Values.FirstOrDefault(value => value.TargetProperty == property.Id)?.Value;
    }

    static bool Handles(ProjectionScopeNode root, SemanticId contract) => root.All.Any(node =>
        node.Scope.Every is { SubscribesToAllEvents: true } || node.Scope.From.Select(from => from.EventContract)
            .Concat(node.Scope.Joins.Select(join => join.EventContract))
            .Concat(node.Scope.Removals.Select(removal => removal.EventContract))
            .Concat(node.Scope.JoinRemovals.Select(removal => removal.EventContract)).Contains(contract));

    void Appended(SemanticSpecificationAppend? appended, JavaCodeBuilder code)
    {
        if (appended is not null) Event(new(appended.EventContract, appended.Values) { EventSource = appended.EventSource }, code, true);
    }

    void Literal(SemanticValue? value, SemanticTypeReference type, JavaCodeBuilder code)
    {
        switch (value)
        {
            case SemanticArrayValue array:
                foreach (var element in array.Values) Literal(element, type with { IsCollection = false, IsOptional = false }, code);
                break;
            case SemanticCompositeValue composite:
                Composite(composite, type, code);
                break;
            case SemanticTextValue or SemanticNumberValue or SemanticBooleanValue when !type.IsCollection:
                types.Claim(type, code);
                break;
            case null:
                ClaimDefault(type, code);
                break;
        }
    }

    void Composite(SemanticCompositeValue value, SemanticTypeReference type, JavaCodeBuilder code)
    {
        types.Claim(type, code);
        var definition = context.Request.Model.Application.Types.First(candidate => candidate.Id == type.Target);
        foreach (var property in definition.Properties) Literal(value.Properties.FirstOrDefault(entry => entry.TargetProperty == property.Id)?.Value, property.Type, code);
    }

    void ClaimDefault(SemanticTypeReference type, JavaCodeBuilder code)
    {
        if (type.IsCollection || _concepts.IsEnumeration(type)) return;
        if (types.PrimitiveOf(type) is SemanticPrimitiveType.WholeNumber or SemanticPrimitiveType.DecimalNumber or SemanticPrimitiveType.Boolean) types.Claim(type, code);
    }

    void Key(SemanticValue key, SemanticReadModel model, JavaCodeBuilder code)
    {
        if (key is not SemanticCompositeValue composite) return;
        var identifiers = model.Properties.Where(property => property.IsIdentifier).ToArray();
        if (identifiers is [{ Type.Kind: SemanticTypeReferenceKind.CompositeType } identifier])
        {
            Literal(key, identifier.Type, code);
            return;
        }
        foreach (var entry in composite.Properties) Literal(entry.Value, model.Properties.First(property => property.Id == entry.TargetProperty).Type, code);
    }
}
