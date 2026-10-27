// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Emitters.Client;

/// <summary>One member of a generated interface.</summary>
/// <param name="Key">The wire key.</param>
/// <param name="Type">The rendered type.</param>
/// <param name="Optional">Whether the key may be missing from a request body.</param>
internal readonly record struct TsMember(string Key, TsType Type, bool Optional = false);

/// <summary>Writes interfaces and their decoders.</summary>
internal static class ClientShapes
{
    internal static void Interface(TsFile file, string name, IReadOnlyList<TsMember> members)
    {
        if (members.Count == 0)
        {
            file.Line($"export interface {name} {{}}").Line();
            return;
        }

        file.Line($"export interface {name} {{");
        foreach (var member in members)
        {
            if (member.Type.Doc is { } doc) file.Line($"    /** {ClientNames.Comment(doc)} */");
            var note = member.Type.Note is { } reason ? $" // Not typed: {ClientNames.Comment(reason)}" : string.Empty;
            file.Line($"    readonly {ClientNames.Member(member.Key)}{(member.Optional ? "?" : string.Empty)}: {member.Type.Type};{note}");
        }

        file.Line("}").Line();
    }

    internal static void Decoder(TsFile file, string decoder, string type, IReadOnlyList<TsMember> members)
    {
        var decodeObject = file.Import("transport.ts", "decodeObject");
        file.Line($"export function {decoder}(value: unknown, path: string): {type} {{");
        if (members.Count == 0)
        {
            file.Line($"    {decodeObject}(value, path);").Line("    return {};").Line("}").Line();
            return;
        }

        file.Line($"    const object = {decodeObject}(value, path);").Line("    return {");
        foreach (var member in members) file.Line($"        {ClientNames.Member(member.Key)}: {member.Type.Decoder}(object[{ClientNames.Literal(member.Key)}], path + {ClientNames.Literal("." + member.Key)}),");
        file.Line("    };").Line("}").Line();
    }
}
