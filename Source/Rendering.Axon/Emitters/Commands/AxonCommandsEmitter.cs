// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands;

internal sealed class AxonCommandsEmitter : IAxonEmitter
{
    public bool IsImplemented => true;

    public void Emit(AxonEmissionContext context)
    {
        foreach (var located in ScopeSelection.Select(context.Request))
        {
            foreach (var command in located.Slice.Commands)
            {
                var source = command.Produces.IsEmpty ? null : CommandSource.Create(context, command, CommandAppends.Contract(context, command.Produces[0].EventContract));
                CommandContractsEmitter.EmitCommand(context, located, command, source);
                if (source is not null) CommandStreamEmitter.Emit(context, located, command, source);
                new CommandHandlerEmitter(context, located, command, source).Emit();
                CommandEndpointEmitter.Emit(context, located, command, source);
                new CommandSpecificationsEmitter(context, located, command, source).Emit();
            }
        }
    }
}
