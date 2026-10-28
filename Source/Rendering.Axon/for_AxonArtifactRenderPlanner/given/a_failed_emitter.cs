// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Emitters;

namespace Cratis.Stage.Rendering.Axon.for_AxonArtifactRenderPlanner.given;

public static class a_failed_emitter
{
    public const string Concept = """
        package proof;
        public record Allocated(String value) {
            public static Allocated create() { return new Allocated("generated"); }
        }
        """;

    public const string Enumeration = """
        package proof;
        public enum Status {
            READY("ready"), DONE("done");
            private final String value;
            Status(String value) { this.value = value; }
            public String value() { return value; }
            public static Status fromValue(String value) {
                for (var candidate : values()) if (candidate.value.equals(value)) return candidate;
                throw new IllegalArgumentException("Unknown Status value: " + value);
            }
        }
        """;

    public const string Incomplete = """
        package proof;
        // Differs from Screenplay: Generation failed; this type is an unsupported stub.
        public class Unfinished {
            public record Value(String value) {}
            public static Value create(String value) { throw new UnsupportedOperationException("Generation failed"); }
        }
        """;

    public static ArtifactRenderPlan Plan(ArtifactRenderRequest request) => new AxonArtifactRenderPlanner([new Failing(), new Dependent()]).Plan(request);

    sealed class Failing : IAxonEmitter
    {
        public bool IsImplemented => true;

        public void Emit(AxonEmissionContext context)
        {
            context.AddJava("proof", "Allocated", Concept);
            context.AddJava("proof", "Status", Enumeration);
            context.AllocateJava("proof", "Unfinished", Incomplete);
            context.Artifacts.Add(PlannedArtifact.CreateText("completed.txt", "completed artifact"));
            context.ConsumedImplementations.Add("partial");
            throw new InvalidOperationException("controlled failure");
        }
    }

    sealed class Dependent : IAxonEmitter
    {
        public bool IsImplemented => true;

        public void Emit(AxonEmissionContext context)
        {
            context.AddJava("proof", "Dependent", """
                package proof;
                public record Dependent(Allocated allocated, Unfinished unfinished) {
                    public String value() { return allocated.value(); }
                    public Allocated generated() { return Allocated.create(); }
                    public Status status() { return Status.fromValue(Status.READY.value()); }
                    public Unfinished.Value nested() { return Unfinished.create(Status.DONE.value()); }
                }
                """);
            context.Artifacts.Add(PlannedArtifact.CreateText("src/main/java/module-info.java", "module proof { exports proof; }\n"));
            if (context.ConsumedImplementations.Contains("partial")) throw new InvalidOperationException("partial implementation consumption survived");
        }
    }
}
