// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views;

internal static class ViewExpectedKeys
{
    internal static string? Expression(SemanticValue key, SemanticReadModel model, ViewJavaTypes types) => key switch
    {
        SemanticTextValue text => JavaCodeBuilder.StringLiteral(text.Value),
        SemanticNumberValue number => JavaCodeBuilder.StringLiteral(number.Value.ToString(CultureInfo.InvariantCulture)),
        SemanticBooleanValue boolean => JavaCodeBuilder.StringLiteral(boolean.Value ? "true" : "false"),
        SemanticCompositeValue composite => Composite(composite, model, types),
        _ => null
    };

    static string Composite(SemanticCompositeValue key, SemanticReadModel model, ViewJavaTypes types)
    {
        var identifiers = model.Properties.Where(property => property.IsIdentifier).ToArray();
        if (identifiers is [{ Type.Kind: SemanticTypeReferenceKind.CompositeType } identifier])
            return $"String.valueOf({types.Literal(key, identifier.Type)})";
        var entries = key.Properties.Select(entry =>
        {
            var property = model.Properties.First(candidate => candidate.Id == entry.TargetProperty);
            return $"java.util.Map.entry({JavaCodeBuilder.StringLiteral(property.Id.ToString())}, String.valueOf({types.Unwrap(property.Type, types.Literal(entry.Value, property.Type))}))";
        });
        return $"java.util.Map.ofEntries({string.Join(", ", entries)})";
    }
}
