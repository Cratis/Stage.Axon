// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands;

internal sealed class AxonCommandsEmitter : IAxonEmitter
{
    public void Emit(AxonEmissionContext context)
    {
        // Do not enable contracts alone as an executable state-change implementation.
        if (!((IAxonEmitter)this).IsImplemented) throw new NotSupportedException("This Axon emitter family has not been implemented.");
        new CommandContractsEmitter().Emit(context);
    }
}
