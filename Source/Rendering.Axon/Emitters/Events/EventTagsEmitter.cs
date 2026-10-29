// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Constraints;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Events;

internal sealed class EventTagsEmitter : IAxonEmitter
{
    public bool IsImplemented => true;

    public void Emit(AxonEmissionContext context)
    {
        foreach (var module in AxonEventsEmitter.Contracts(context).Select(pair => pair.Module).DistinctBy(module => module.Id))
        {
            var contracts = Contracts(context, module).ToArray();
            var code = new JavaCodeBuilder(AxonEventsEmitter.Package(context, module)).Declare("EventTags")
                .Block("public final class EventTags", body =>
                {
                    foreach (var (key, name) in Constants(context, contracts).OrderBy(pair => pair.Value, StringComparer.Ordinal))
                        body.Line($"public static final String {name} = {JavaCodeBuilder.StringLiteral(key)};");
                    body.Line("private EventTags() {}");
                }).Build();
            context.AddJava(AxonEventsEmitter.Package(context, module), "EventTags", code, sources: [.. contracts.Select(contract => contract.Id)]);
        }
    }

    internal static string Reference(AxonEmissionContext context, SemanticEventContract contract) =>
        Reference(context, contract, AxonEventSources.PrimaryTag(context, contract).TagKey);

    internal static string Reference(AxonEmissionContext context, SemanticEventContract contract, string key)
    {
        var module = ScopeSelection.Locate(context.Request.Model.Application).First(slice => slice.Slice.Events.Any(candidate => candidate.Id == contract.Id)).Module;
        return AxonEventsEmitter.Package(context, module) + ".EventTags." + Constants(context, Contracts(context, module))[key];
    }

    /// <summary>The tag constant a set-validation entity is bounded by: the owner tag for occurrences, the constraint tag for values.</summary>
    internal static string ConstraintReference(AxonEmissionContext context, SemanticConstraint constraint, SemanticEventContract contract, JavaCodeBuilder code, string package) =>
        constraint.Kind == SemanticConstraintKind.UniqueEventOccurrence ? Reference(context, contract, code, package) : Reference(Reference(context, contract, constraint.Name), code, package);

    /// <summary>The tag annotations a record component carries for the property-uniqueness constraints it claims or releases.</summary>
    internal static string ConstraintTags(AxonEmissionContext context, SemanticEventContract contract, SemanticProperty property, JavaCodeBuilder code, string package) =>
        string.Concat(ConstraintCoverage.Tagged(context, contract).Where(item => item.Property.Id == property.Id && ConstraintCoverage.Normalized(context, item.Constraint, item.Property.Type, "x") is null)
            .Select(item => $"@{code.Reference("org.axonframework.eventsourcing.annotation.EventTag")}(key = {Reference(Reference(context, contract, item.Constraint.Name), code, package)}) "));

    /// <summary>The record members that expose a normalized tag value for case-insensitive constraints.</summary>
    internal static IReadOnlyList<string> ConstraintMembers(AxonEmissionContext context, SemanticEventContract contract, JavaCodeBuilder code, string package)
    {
        var members = new List<string>();
        var names = new HashSet<string>(contract.Properties.Select(property => JavaNames.Member(property.Name)), StringComparer.Ordinal);
        foreach (var (constraint, property) in ConstraintCoverage.Tagged(context, contract).Where(item => ConstraintCoverage.Normalized(context, item.Constraint, item.Property.Type, "x") is not null))
        {
            var name = JavaNames.Member(property.Name) + "UniqueKey";
            while (!names.Add(name)) name += "_";
            var tag = Reference(Reference(context, contract, constraint.Name), code, package);
            var guard = property.Type.IsOptional ? $"{JavaNames.Member(property.Name)} == null ? null : " : string.Empty;
            members.Add($"@{code.Reference("org.axonframework.eventsourcing.annotation.EventTag")}(key = {tag}) public {code.Reference("java.lang.String")} {name}() {{ return {guard}{code.ReferenceExpression(ConstraintCoverage.Normalized(context, constraint, property.Type, JavaNames.Member(property.Name))!)}; }}");
        }
        return members;
    }

    internal static string Reference(AxonEmissionContext context, SemanticEventContract contract, JavaCodeBuilder code, string package)
        => Reference(Reference(context, contract), code, package);

    internal static string Reference(string reference, JavaCodeBuilder code, string package)
    {
        var separator = reference.LastIndexOf('.');
        var type = reference[..separator];
        var typeSeparator = type.LastIndexOf('.');
        var name = type[(typeSeparator + 1)..];
        if (type[..typeSeparator] == package) code.Declare(name);
        else return code.Reference(type) + reference[separator..];
        return name + reference[separator..];
    }

    static IEnumerable<SemanticEventContract> Contracts(AxonEmissionContext context, SemanticModule module) =>
        ScopeSelection.Locate(context.Request.Model.Application).Where(slice => slice.Module.Id == module.Id)
            .SelectMany(slice => slice.Slice.Events).DistinctBy(contract => contract.Id);

    static Dictionary<string, string> Constants(AxonEmissionContext context, IEnumerable<SemanticEventContract> contracts) =>
        EventTagNames.Constants(contracts.SelectMany(contract => ConstraintCoverage.Tagged(context, contract).Select(item => item.Constraint.Name)
            .Concat([AxonEventSources.PrimaryTag(context, contract).TagKey])));
}
