// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Concepts;

namespace Cratis.Stage.Rendering.Axon.Emitters.Client;

/// <summary>Writes the concept aliases, enumeration unions and composite types shared by every slice module.</summary>
internal static class ClientConcepts
{
    internal static void Write(AxonEmissionContext context, TsFile file, ClientTypes types)
    {
        var application = context.Request.Model.Application;
        foreach (var concept in application.Concepts)
        {
            if (concept.Values.IsDefaultOrEmpty) Scalar(file, types, concept);
            else Enumeration(file, types, concept);
        }

        foreach (var composite in application.Types)
        {
            var (name, decoder) = types.Declared(composite.Id);
            var members = composite.Properties.Select(property => new TsMember(property.Name, types.Resolve(file, property.Type, property.Id, $"{composite.Name}.{property.Name}"))).ToArray();
            ClientShapes.Interface(file, name, members);
            ClientShapes.Decoder(file, decoder, name, members);
        }
    }

    static void Scalar(TsFile file, ClientTypes types, SemanticConcept concept)
    {
        var (name, decoder) = types.Declared(concept.Id);
        var primitive = types.Primitive(file, concept.Primitive, concept.Id, "concept " + concept.Name);
        if (primitive.Doc is { } doc) file.Line($"/** {ClientNames.Comment(doc)} */");
        file.Line($"export type {name} = {primitive.Type};" + (primitive.Note is { } reason ? $" // Not typed: {ClientNames.Comment(reason)}" : string.Empty))
            .Line($"export function {decoder}(value: unknown, path: string): {name} {{ return {primitive.Decoder}(value, path); }}")
            .Line();
    }

    static void Enumeration(TsFile file, ClientTypes types, SemanticConcept concept)
    {
        var (name, decoder) = types.Declared(concept.Id);
        var values = file.Declare(ClientNames.Value(concept.Name) + "Values");
        var numeric = ConceptEnums.IsNumeric(concept);
        var literals = Enumerable.Range(0, concept.Values.Length)
            .Select(index => numeric ? concept.Numbers!.Value[index].ToString(CultureInfo.InvariantCulture) : ClientNames.Literal(concept.Values[index])).ToArray();
        var decodeOneOf = file.Import("transport.ts", "decodeOneOf");
        file.Line($"export type {name} = {string.Join(" | ", literals)};")
            .Line($"const {values} = [{string.Join(", ", literals)}] as const;")
            .Line($"export function {decoder}(value: unknown, path: string): {name} {{ return {decodeOneOf}(value, path, {values}); }}")
            .Line();
    }
}
