// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;

namespace Cratis.Stage.Rendering.Axon.Emitters.Client;

/// <summary>A modeled type as rendered in TypeScript.</summary>
/// <param name="Type">The TypeScript type.</param>
/// <param name="Decoder">The decoder expression: a function taking the value and its path.</param>
/// <param name="Doc">A doc comment for members of this type, when the wire loses precision.</param>
/// <param name="Note">The reason the type could not be modeled, when it is <c>unknown</c>.</param>
internal sealed record TsType(string Type, string Decoder, string? Doc = null, string? Note = null);

/// <summary>Maps semantic types to TypeScript types and decoders, and reports the shapes that cannot be typed.</summary>
internal sealed class ClientTypes
{
    internal const string ConceptsModule = "concepts.ts";

    const string WholeDoc = "Travels as a JSON number; a decoded value outside the safe integer range is rejected.";
    const string DecimalDoc = "Travels as a JSON number and is decoded as an IEEE 754 double, so precision beyond about 15 significant digits is lost.";

    readonly AxonEmissionContext _context;
    readonly Dictionary<SemanticId, (string Type, string Decoder)> _declared = [];
    readonly HashSet<(SemanticId Construct, string Reason)> _warned = [];

    internal ClientTypes(AxonEmissionContext context, TsFile concepts)
    {
        _context = context;
        var application = context.Request.Model.Application;
        foreach (var concept in application.Concepts) _declared[concept.Id] = (concepts.Declare(ClientNames.Type(concept.Name)), string.Empty);
        foreach (var composite in application.Types) _declared[composite.Id] = (concepts.Declare(ClientNames.Type(composite.Name)), string.Empty);
        foreach (var id in _declared.Keys.ToArray()) _declared[id] = (_declared[id].Type, concepts.Declare("decode" + _declared[id].Type));
    }

    /// <summary>Gets the names a concept or composite type is declared under in the concepts module.</summary>
    /// <param name="id">The concept or composite identifier.</param>
    /// <returns>The type and decoder names.</returns>
    internal (string Type, string Decoder) Declared(SemanticId id) => _declared[id];

    /// <summary>Renders a property or parameter type.</summary>
    /// <param name="file">The module the type is used in; imports are added to it.</param>
    /// <param name="type">The modeled type.</param>
    /// <param name="owner">The construct that declares the type.</param>
    /// <param name="where">A readable location for diagnostics.</param>
    /// <param name="decode">Whether the decoder is used; a type-only use does not import it.</param>
    /// <returns>The rendered type.</returns>
    internal TsType Resolve(TsFile file, SemanticTypeReference type, SemanticId owner, string where, bool decode = true)
    {
        var item = Item(file, type, owner, where, decode);
        var text = item.Type;
        var decoder = item.Decoder;
        if (type.IsCollection)
        {
            text += "[]";
            decoder = decode ? $"{file.Import("transport.ts", "arrayOf")}({decoder})" : decoder;
        }

        if (type.IsOptional)
        {
            text += " | null";
            decoder = decode ? $"{file.Import("transport.ts", "nullable")}({decoder})" : decoder;
        }

        return item with { Type = text, Decoder = decoder };
    }

    /// <summary>Renders a primitive.</summary>
    /// <param name="file">The module the type is used in.</param>
    /// <param name="primitive">The primitive.</param>
    /// <param name="owner">The construct that declares the type, for diagnostics.</param>
    /// <param name="where">A readable location for diagnostics.</param>
    /// <param name="decode">Whether the decoder is used; a type-only use does not import it.</param>
    /// <returns>The rendered type.</returns>
    internal TsType Primitive(TsFile file, SemanticPrimitiveType primitive, SemanticId owner, string where, bool decode = true)
    {
        string Decode(string name) => decode ? file.Import("transport.ts", name) : string.Empty;
        switch (primitive)
        {
            case SemanticPrimitiveType.Uuid or SemanticPrimitiveType.Text or SemanticPrimitiveType.Date or SemanticPrimitiveType.DateTime:
                return new("string", Decode("decodeString"));
            case SemanticPrimitiveType.Boolean:
                return new("boolean", Decode("decodeBoolean"));
            case SemanticPrimitiveType.WholeNumber:
                Warn(owner, where, "whole numbers travel as JSON numbers, so a value beyond 2^53 - 1 is rejected by the generated TypeScript decoder");
                return new("number", Decode("decodeSafeInteger"), WholeDoc);
            case SemanticPrimitiveType.DecimalNumber:
                Warn(owner, where, "decimal numbers travel as JSON numbers and are decoded as doubles, so precision beyond about 15 significant digits is lost");
                return new("number", Decode("decodeNumber"), DecimalDoc);
            default:
                return Unknown(file, owner, where, $"the primitive type '{primitive}' is not known to the TypeScript client", decode);
        }
    }

    internal TsType Unknown(TsFile file, SemanticId owner, string where, string reason, bool decode = true)
    {
        _context.Degradation.Unsupported(AxonRefusalCodes.Client, owner, $"{where}: {reason}.", "type the modeled value in the TypeScript client", owner + "/client");
        return new("unknown", decode ? file.Import("transport.ts", "decodeUnknown") : string.Empty, null, reason);
    }

    internal void Warn(SemanticId owner, string where, string reason)
    {
        var text = $"{where}: {reason}.";
        if (_warned.Add((owner, text))) _context.Degradation.Warn(AxonRefusalCodes.ClientPrecision, owner, text);
    }

    TsType Item(TsFile file, SemanticTypeReference type, SemanticId owner, string where, bool decode)
    {
        if (type.Kind == SemanticTypeReferenceKind.Primitive) return Primitive(file, type.Primitive, owner, where, decode);
        if (type.Kind is SemanticTypeReferenceKind.Concept or SemanticTypeReferenceKind.CompositeType && _declared.TryGetValue(type.Target, out var names))
            return new(file.Import(ConceptsModule, names.Type, true), decode ? file.Import(ConceptsModule, names.Decoder) : string.Empty);
        return Unknown(file, owner, where, type.Kind is SemanticTypeReferenceKind.Concept or SemanticTypeReferenceKind.CompositeType ? "the referenced concept or type is not part of the model" : $"the type kind '{type.Kind}' is not known to the TypeScript client", decode);
    }
}
