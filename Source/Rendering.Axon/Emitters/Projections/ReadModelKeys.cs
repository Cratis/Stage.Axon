// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal static class ReadModelKeys
{
    internal static IReadOnlyList<KeyPart>? Parts(SemanticApplication application, SemanticReadModel model)
    {
        var identifiers = model.Properties.Where(property => property.IsIdentifier).ToArray();
        if (identifiers is [{ Type.Kind: SemanticTypeReferenceKind.CompositeType } identifier])
            return application.Types.FirstOrDefault(type => type.Id == identifier.Type.Target)?.Properties.Select(property => new KeyPart(property)).ToArray();
        return identifiers.Length > 1 ? identifiers.Select(property => new KeyPart(property)).ToArray() : null;
    }

    internal static bool IsComposite(SemanticApplication application, SemanticReadModel model) => Parts(application, model) is not null;

    internal static string KeyName(SemanticReadModel model) => JavaNames.Type(model.Name) + "Key";

    internal static void Emit(AxonEmissionContext context, ViewJavaTypes types, string package, SemanticReadModel model)
    {
        var application = context.Request.Model.Application;
        if (Parts(application, model) is not { } parts) return;
        var name = KeyName(model);
        var code = new JavaCodeBuilder(package).Declare(name);
        var mapping = new ReadModelEntities.Columns(application, types, code);
        var components = parts.Select(part =>
        {
            var column = mapping.Describe(part.Property, "k_");
            return $"{string.Join(' ', column.Annotations)} {column.Type} {JavaNames.Member(part.Property.Name)}";
        }).ToArray();
        code.Line($"@{code.Reference("jakarta.persistence.Embeddable")}")
            .Line($"public record {name}({string.Join(", ", components)}) implements {code.Reference("java.io.Serializable")} {{}}");
        ViewJavaTypes.Add(context, package, name, code.Build(), model.Id);
    }

    internal sealed record KeyPart(SemanticProperty Property);
}
