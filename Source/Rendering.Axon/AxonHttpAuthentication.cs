// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon;

/// <summary>
/// Defines HTTP authentication for the generated Axon application.
/// </summary>
public enum AxonHttpAuthentication
{
    /// <summary>
    /// Uses HTTP Basic authentication, the default.
    /// </summary>
    Basic = 0,

    /// <summary>
    /// Uses bearer-token authentication, as requested by <see cref="AxonRendering.WithBearerTokens"/>.
    /// </summary>
    BearerTokens = 1
}
