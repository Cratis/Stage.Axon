// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Runtime;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries;

internal static class LiveQueryStreams
{
    internal static void Emit(AxonEmissionContext context, string package, SemanticKeyedQuery query, string modelType)
    {
        var name = JavaNames.Type(query.Name) + "Streams";
        var code = QueryModelReference.Create(context, package).Declare(JavaNames.Type(query.Name)).Declare(name);
        QueryModelReference.ReserveLocal(code, package, modelType, JavaNames.Type(query.Name), name);
        var live = code.Reference(AxonRuntimePackage.Type(context.BasePackage, "LiveStreams"));
        code.Line($$"""
            @{{code.Reference("org.springframework.stereotype.Component")}}({{JavaCodeBuilder.StringLiteral(package + "." + name)}})
            public final class {{name}} {
                private final {{live}}.Group group;

                public {{name}}({{live}} live) { this.group = live.group(); }

                public {{code.Reference("org.springframework.web.servlet.mvc.method.annotation.SseEmitter")}} open({{code.Reference("java.util.concurrent.Flow")}}.Publisher<{{code.Reference("java.lang.Object")}}> publisher) {
                    return group.open(publisher);
                }

                int activeCount() { return group.activeCount(); }
            }
            """);
        context.AddJava(package, name, code.Build(), sources: query.Id);
    }
}
