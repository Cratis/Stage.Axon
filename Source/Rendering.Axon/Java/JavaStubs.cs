// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Java;

internal static class JavaStubs
{
    internal static string Interface(string package, string name) =>
        new JavaCodeBuilder(package).Block($"public interface {JavaNames.Type(name)}", _ => { }).Build();

    internal static string ThrowingBody(string code, string reason) =>
        "{\n    throw new UnsupportedOperationException(" + JavaCodeBuilder.StringLiteral($"{code}: {reason}") + ");\n}";
}
