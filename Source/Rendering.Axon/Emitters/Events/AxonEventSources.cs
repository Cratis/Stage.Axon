// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Concepts;
using Cratis.Stage.Rendering.Axon.Emitters.Runtime;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Events;

internal static class AxonEventSources
{
    internal static (string ComponentName, string JavaType, string TagKey) PrimaryTag(AxonEmissionContext context, SemanticEventContract contract)
    {
        var producers = ScopeSelection.Locate(context.Request.Model.Application).SelectMany(slice => slice.Slice.Commands)
            .SelectMany(command => command.Produces.Where(produced => produced.EventContract == contract.Id).Select(produced => (Command: command, Produced: produced)));
        var identifier = producers.Select(pair => FromProducer(context, pair.Command, pair.Produced)).FirstOrDefault(concept => concept is not null)
            ?? Identifier(context, contract.Properties);
        return identifier is not null ? Tag(context, identifier) : ("eventSourceId", AxonRuntimePackage.Type(context.BasePackage, "EventSourceId"), "eventSourceId");
    }

    internal static string Record(AxonEmissionContext context, string package, SemanticEventContract contract, string annotation) =>
        Record(context, package, contract, _ => annotation);

    internal static string Record(AxonEmissionContext context, string package, SemanticEventContract contract, Func<JavaCodeBuilder, string> annotation)
    {
        var primary = PrimaryTag(context, contract);
        var code = new JavaCodeBuilder(package).Declare(JavaNames.Type(contract.Name));
        var tag = EventTagsEmitter.Reference(context, contract, code, package);
        var addPrimary = !contract.Properties.Any(property => JavaNames.Member(property.Name) == primary.ComponentName);
        var primaryType = addPrimary ? code.Reference(primary.JavaType) : string.Empty;
        foreach (var property in contract.Properties) JavaSemanticTypes.Claim(context, property.Type, code, package);
        var parameters = contract.Properties.Select(property => Component(context, property, (primary.ComponentName, tag), code)).ToList();
        if (addPrimary)
            parameters.Insert(0, $"@{code.Reference("org.axonframework.eventsourcing.annotation.EventTag")}(key = {tag}) {primaryType} {primary.ComponentName}");
        var extraTags = contract.Tags.Concat(contract.Properties.Where(property => property.IsIdentifier).Select(property => property.Name))
            .Where(tag => tag != primary.TagKey).Distinct(StringComparer.Ordinal).ToArray();
        if (extraTags.Length > 0)
        {
            const string diagnostic = "STAGE-AXON-EVENT-TAGS";
            const string intent = "tag the event with additional modeled identities";
            var path = contract.Id + "/tags";
            context.Degradation.Unsupported(diagnostic, contract.Id, "Axon events use exactly one primary event-source tag; extra 0202 model tags are omitted: " + string.Join(", ", extraTags), intent, path);
            code.DiffersFromScreenplay("Only the primary event-source component is tagged; extra model tags are omitted.")
                .UnsupportedIntent(intent, path, diagnostic);
        }
        return code.Line(annotation(code)).Line($"public record {JavaNames.Type(contract.Name)}({string.Join(", ", parameters)}) {{}}").Build();
    }

    static SemanticConcept? FromProducer(AxonEmissionContext context, SemanticCommand command, SemanticProducedEvent produced) =>
        Concept(context, produced.DestinationType) ?? Concept(context, command.Destination?.Type)
        ?? FromRoute(context, produced.Route ?? command.Route) ?? Identifier(context, command.Properties);

    static SemanticConcept? FromRoute(AxonEmissionContext context, SemanticCommandRoute? route)
    {
        if (route is null) return null;
        var source = context.Request.Model.Application.EventSources.FirstOrDefault(source => source.Id == route.Source);
        return Concept(context, source?.IdentifierType);
    }

    static SemanticConcept? Identifier(AxonEmissionContext context, IEnumerable<SemanticProperty> properties) =>
        properties.Where(property => property.IsIdentifier).Select(property => Concept(context, property.Type)).FirstOrDefault(concept => concept is not null);

    static SemanticConcept? Concept(AxonEmissionContext context, SemanticTypeReference? type) =>
        type?.Kind == SemanticTypeReferenceKind.Concept ? context.Request.Model.Application.Concepts.FirstOrDefault(concept => concept.Id == type.Target) : null;

    static (string ComponentName, string JavaType, string TagKey) Tag(AxonEmissionContext context, SemanticConcept concept)
    {
        var name = JavaNames.Member(concept.Name);
        return (name, context.BasePackage + ".concepts." + JavaNames.Type(concept.Name), name);
    }

    static string Component(AxonEmissionContext context, SemanticProperty property, (string ComponentName, string Reference) primary, JavaCodeBuilder code)
    {
        var name = JavaNames.Member(property.Name);
        var tag = name == primary.ComponentName ? $"@{code.Reference("org.axonframework.eventsourcing.annotation.EventTag")}(key = {primary.Reference}) " : string.Empty;
        var json = JavaJsonNames.Annotation(property.Name, code);
        return tag + $"{json}{JavaSemanticTypes.TypeFor(context, property.Type, code)} {name}";
    }
}
