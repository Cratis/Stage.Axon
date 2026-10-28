// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Emitters;

namespace Cratis.Stage.Rendering.Axon.for_AxonArtifactRenderPlanner.given;

public static class a_failed_emitter
{
    public static ArtifactRenderPlan Plan(ArtifactRenderRequest request) => new AxonArtifactRenderPlanner([new Failing(), new Dependent()]).Plan(request);

    sealed class Failing : IAxonEmitter
    {
        public bool IsImplemented => true;

        public void Emit(AxonEmissionContext context)
        {
            context.AddJava("proof", "Allocated", "package proof;\npublic record Allocated(String value) {}\n");
            context.AllocateJava("proof", "Unfinished");
            context.Artifacts.Add(PlannedArtifact.CreateText("partial.txt", "must not survive"));
            context.ConsumedImplementations.Add("partial");
            throw new InvalidOperationException("controlled failure");
        }
    }

    sealed class Dependent : IAxonEmitter
    {
        public bool IsImplemented => true;

        public void Emit(AxonEmissionContext context)
        {
            context.AddJava("proof", "Dependent", "package proof;\npublic record Dependent(Allocated allocated, Unfinished unfinished) { public String value() { return allocated.value(); } }\n");
            context.Artifacts.Add(PlannedArtifact.CreateText("src/main/java/module-info.java", "module proof { exports proof; }\n"));
            if (context.ConsumedImplementations.Contains("partial")) throw new InvalidOperationException("partial implementation consumption survived");
        }
    }
}
