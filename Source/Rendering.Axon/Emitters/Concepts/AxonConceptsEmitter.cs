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

    internal static string Record(AxonEmissionContext context, string package, string name, IEnumerable<SemanticProperty> properties)
    {
        var code = new JavaCodeBuilder(package).Declare(name);
        foreach (var composite in context.Request.Model.Application.Types) code.Declare(JavaNames.Type(composite.Name));
        var fields = properties.ToArray();
        foreach (var property in fields) JavaSemanticTypes.Claim(context, property.Type, code, package);
        var parameters = string.Join(", ", fields.Select(property => $"{JavaJsonNames.Annotation(property.Name, code)}{JavaSemanticTypes.TypeFor(context, property.Type, code, package)} {JavaNames.Member(property.Name)}"));
        return code.Line($"public record {name}({parameters}) {{}}").Build();
    }

    internal static string Scalar(AxonEmissionContext context, SemanticConcept concept)
    {
        var name = JavaNames.Type(concept.Name);
        var builder = JavaSemanticTypes.ConceptBuilder(context, name);
        var primitive = JavaSemanticTypes.Primitive(concept.Primitive, builder);
        var marker = concept.Generation is null ? string.Empty : $", {builder.Reference(CratisFundamentals.ConceptsPackage + ".Generatable")}<{name}>";
        return builder
            .Block($"public record {name}({primitive} value) implements {builder.Reference(CratisFundamentals.ConceptsPackage + ".ConceptAs")}<{primitive}>{marker}", body =>
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
                            .Line($"throw new {constructor.Reference("java.lang.UnsupportedOperationException")}(" + JavaCodeBuilder.StringLiteral(code + ": " + intent) + ");");
                    }
                }).Line("@" + body.Reference("java.lang.Override")).Line($"public {body.Reference("java.lang.String")} toString() {{ return value.toString(); }}");
                ConceptGeneration.Emit(context, concept, body);
            }).Build();
    }
}
