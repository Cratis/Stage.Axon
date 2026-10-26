// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views;

internal static class ViewExpectedKeys
{
    internal static string? Expression(SemanticValue key, SemanticReadModel model, ViewJavaTypes types, JavaCodeBuilder? code = null) => key switch
    {
        SemanticTextValue text => JavaCodeBuilder.StringLiteral(text.Value),
        SemanticNumberValue number => JavaCodeBuilder.StringLiteral(number.Value.ToString(CultureInfo.InvariantCulture)),
        SemanticBooleanValue boolean => JavaCodeBuilder.StringLiteral(boolean.Value ? "true" : "false"),
        SemanticCompositeValue composite => Composite(composite, model, types, code),
        _ => null
    };

    static string Composite(SemanticCompositeValue key, SemanticReadModel model, ViewJavaTypes types, JavaCodeBuilder? code)
    {
        var identifiers = model.Properties.Where(property => property.IsIdentifier).ToArray();
        if (identifiers is [{ Type.Kind: SemanticTypeReferenceKind.CompositeType } identifier])
            return $"String.valueOf({(code is null ? types.Literal(key, identifier.Type) : types.Literal(key, identifier.Type, code))})";
        var map = code?.Reference("java.util.Map") ?? "java.util.Map";
        var entries = key.Properties.Select(entry =>
        {
            var property = model.Properties.First(candidate => candidate.Id == entry.TargetProperty);
            return $"{map}.entry({JavaCodeBuilder.StringLiteral(property.Id.ToString())}, String.valueOf({types.Unwrap(property.Type, code is null ? types.Literal(entry.Value, property.Type) : types.Literal(entry.Value, property.Type, code))}))";
        });
        return $"{map}.ofEntries({string.Join(", ", entries)})";
    }
}
