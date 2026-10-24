// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Concepts;

internal sealed class AxonConceptsEmitter : IAxonEmitter
{
    public bool IsImplemented => true;

    public void Emit(AxonEmissionContext context)
    {
        foreach (var concept in context.Request.Model.Application.Concepts)
        {
            var code = concept.Values.IsDefaultOrEmpty ? Scalar(context, concept) : ConceptEnums.Emit(context, concept);
            context.AddJava(context.BasePackage + ".concepts", JavaNames.Type(concept.Name), code, sources: [concept.Id]);
        }
        foreach (var composite in context.Request.Model.Application.Types)
        {
            var name = JavaNames.Type(composite.Name);
            context.AddJava(context.BasePackage + ".types", name, Record(context, context.BasePackage + ".types", name, composite.Properties), sources: [composite.Id]);
        }
    }

    internal static string Record(AxonEmissionContext context, string package, string name, IEnumerable<SemanticProperty> properties, string annotation = "")
    {
        var parameters = string.Join(", ", properties.Select(property => $"@com.fasterxml.jackson.annotation.JsonProperty({JavaCodeBuilder.StringLiteral(property.Name)}) {JavaSemanticTypes.TypeFor(context, property.Type)} {JavaNames.Member(property.Name)}"));
        var code = new JavaCodeBuilder(package);
        if (annotation.Length > 0) code.Line(annotation);
        return code.Line($"public record {name}({parameters}) {{}}").Build();
    }

    static string Scalar(AxonEmissionContext context, SemanticConcept concept)
    {
        var name = JavaNames.Type(concept.Name);
        var primitive = JavaSemanticTypes.Primitive(concept.Primitive);
        return new JavaCodeBuilder(context.BasePackage + ".concepts")
            .Block($"public record {name}({primitive} value) implements {context.BasePackage}.runtime.ConceptAs<{primitive}>", body =>
            {
                body.Block($"public {name}", constructor =>
                {
                    ConceptValidations.Emit(context, concept, constructor);
                    if (concept.Compliance is { Personal: not null } or { Secret: not null })
                    {
                        const string code = "STAGE-AXON-COMPLIANCE";
                        const string intent = "protect personal or secret concept values";
                        var path = concept.Id + "/compliance";
                        context.Degradation.Unsupported(code, concept.Id, "Protection at rest is not configured; constructing this concept is blocked.", intent, path);
                        constructor.UnsupportedIntent(intent, path, code)
                            .Line("throw new UnsupportedOperationException(" + JavaCodeBuilder.StringLiteral(code + ": " + intent) + ");");
                    }
                });
                ConceptGeneration.Emit(context, concept, body);
            }).Build();
    }
}
