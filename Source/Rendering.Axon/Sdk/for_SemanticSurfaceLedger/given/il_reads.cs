// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Reflection.Emit;

namespace Cratis.Stage.Rendering.Axon.Sdk.for_SemanticSurfaceLedger.given;

internal static class il_reads
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
    static readonly IReadOnlyDictionary<short, OpCode> Opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode)).Select(field => (OpCode)field.GetValue(null)!).ToDictionary(opcode => opcode.Value);

    static readonly IReadOnlyDictionary<OperandType, int> Sizes = new Dictionary<OperandType, int>
    {
        [OperandType.InlineNone] = 0,
        [OperandType.ShortInlineBrTarget] = 1,
        [OperandType.ShortInlineI] = 1,
        [OperandType.ShortInlineVar] = 1,
        [OperandType.InlineVar] = 2,
        [OperandType.InlineI8] = 8,
        [OperandType.InlineR] = 8,
        [OperandType.InlineBrTarget] = 4,
        [OperandType.InlineField] = 4,
        [OperandType.InlineI] = 4,
        [OperandType.InlineMethod] = 4,
        [OperandType.InlineSig] = 4,
        [OperandType.InlineString] = 4,
        [OperandType.InlineTok] = 4,
        [OperandType.InlineType] = 4,
        [OperandType.ShortInlineR] = 4
    };

    internal static bool Reads(Type reader, Type declaring, string property) => Types(reader).Any(type => Methods(type).Any(method => Reads(method, declaring, property)));

    static IEnumerable<Type> Types(Type type) => new[] { type }.Concat(type.GetNestedTypes(All).SelectMany(Types));

    static IEnumerable<MethodBase> Methods(Type type) => type.GetMethods(All).Cast<MethodBase>().Concat(type.GetConstructors(All))
        .Concat(type.TypeInitializer is { } initializer ? new[] { initializer } : []);

    static bool Reads(MethodBase method, Type declaring, string property)
    {
        var bytes = method.GetMethodBody()?.GetILAsByteArray();
        if (bytes is null) return false;
        for (var position = 0; position < bytes.Length;)
        {
            var value = bytes[position++];
            var opcode = Opcodes[value == 0xfe ? unchecked((short)(0xfe00 | bytes[position++])) : value];
            if (opcode.OperandType == OperandType.InlineMethod && Matches(method, BitConverter.ToInt32(bytes, position), declaring, property)) return true;
            position += OperandSize(opcode.OperandType, bytes, position);
        }
        return false;
    }

    static bool Matches(MethodBase caller, int token, Type declaring, string property)
    {
        try
        {
            var target = caller.Module.ResolveMethod(token, caller.DeclaringType?.GetGenericArguments(), caller.IsGenericMethod ? caller.GetGenericArguments() : null);
            return target?.DeclaringType == declaring && (target.Name == "get_" + property || target.Name == "Deconstruct");
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    static int OperandSize(OperandType operand, byte[] bytes, int position)
    {
        if (operand == OperandType.InlineSwitch) return 4 + (4 * BitConverter.ToInt32(bytes, position));
        return Sizes.TryGetValue(operand, out var size) ? size : throw new InvalidOperationException($"Unexpected IL operand {operand}.");
    }
}
