// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Specifications;

internal sealed class AxonSpecificationsEmitter : IAxonEmitter
{
    const string UnsupportedCode = "STAGE-AXON-SPEC";

    public bool IsImplemented => true;

    public void Emit(AxonEmissionContext context)
    {
        var unclaimed = ScopeSelection.Select(context.Request).SelectMany(slice => slice.Slice.Specifications).Where(spec => !context.Specifications.ContainsKey(spec.Id)).ToArray();
        if (unclaimed.Length > 0) EmitUnclaimed(context, unclaimed);
        var lines = context.Specifications.Values.OrderBy(entry => entry.Id.ToString(), StringComparer.Ordinal).Select(entry =>
            entry.Id + "\t" + (entry.DisabledCode is null ? string.Empty : "disabled " + entry.DisabledCode + "\t") + entry.TestClass + "#" + entry.Method);
        context.Artifacts.Add(PlannedArtifact.CreateText("stage-specifications.txt", string.Join('\n', lines) + "\n"));
    }

    internal static void Register(AxonEmissionContext context, SemanticId id, string testClass, string method, string? disabledCode = null) =>
        context.Specifications.Add(id, new(id, testClass, method, disabledCode));

    static void EmitUnclaimed(AxonEmissionContext context, SemanticSpecification[] specifications)
    {
        var package = context.BasePackage + ".specifications";
        const string name = "UnimplementedSpecificationsTest";
        var code = new JavaCodeBuilder(package)
            .Import("org.junit.jupiter.api.Disabled")
            .Import("org.junit.jupiter.api.DisplayName")
            .Import("org.junit.jupiter.api.Test")
            .Block("class " + name, body =>
            {
                foreach (var specification in specifications)
                {
                    var method = "spec_" + JavaNames.Member(specification.Id.ToString());
                    var intent = "verify " + specification.Name;
                    var path = specification.Id.ToString();
                    context.Degradation.Unsupported(UnsupportedCode, specification.Id, "No Axon specification renderer has claimed this specification.", intent, path);
                    Register(context, specification.Id, package + "." + name, method, UnsupportedCode);
                    body.UnsupportedIntent(intent, path, UnsupportedCode)
                        .Line("@Test")
                        .Line("@DisplayName(" + JavaCodeBuilder.StringLiteral(path) + ")")
                        .Line("@Disabled(" + JavaCodeBuilder.StringLiteral(UnsupportedCode + ": " + intent) + ")")
                        .Block("void " + method + "()", test => test.Line("throw new UnsupportedOperationException(" + JavaCodeBuilder.StringLiteral(UnsupportedCode + ": " + intent) + ");"));
                }
            });
        context.AddJava(package, name, code.Build(), test: true, sources: [.. specifications.Select(specification => specification.Id)]);
    }
}
