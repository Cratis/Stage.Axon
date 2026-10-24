// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Verification;

internal sealed record DisabledTestBudget(int Count, string Reason)
{
    internal bool Valid => Count > 0 && !string.IsNullOrWhiteSpace(Reason);
}
