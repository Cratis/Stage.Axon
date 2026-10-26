// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries;

/// <summary>Claims a read model without importing its own package or shadowing a declared query type.</summary>
internal static class QueryModelReference
{
    internal static string Claim(JavaCodeBuilder code, string package, string modelType, params string[] declared)
    {
        var separator = modelType.LastIndexOf('.');
        var name = modelType[(separator + 1)..];
        if (modelType[..separator] != package || declared.Contains(name, StringComparer.Ordinal)) return code.Reference(modelType);
        code.Declare(name);
        return name;
    }
}
