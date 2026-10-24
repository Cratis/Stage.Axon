// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Emitters;

/// <summary>A family opts in when its implementation is ready; unimplemented seams cannot silently emit nothing.</summary>
internal interface IAxonEmitter
{
    bool IsImplemented => false;

    void Emit(AxonEmissionContext context) => throw new NotSupportedException("This Axon emitter family has not been implemented.");
}
