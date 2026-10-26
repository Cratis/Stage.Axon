// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Java;

/// <summary>
/// The exception that is thrown when an emitter claims a Java simple name that another type in the same file already holds.
/// </summary>
/// <param name="simpleName">The simple name being claimed.</param>
/// <param name="existing">The type that already holds the name, or empty for a declared type.</param>
public class SimpleNameAlreadyClaimed(string simpleName, string existing)
    : Exception($"The simple name '{simpleName}' is already used for {(existing.Length == 0 ? "a declared type" : $"'{existing}'")}. Claim application type names before referencing framework types.");
