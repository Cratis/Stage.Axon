// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Specifications;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views;

internal static class ReducerViewSpecifications
{
    internal static void Emit(AxonEmissionContext context, ViewJavaTypes types, LocatedSemanticSlice located, SemanticReadModel model, SemanticSpecification[] specs)
    {
        var package = types.Package(located);
        var name = JavaNames.Type(model.Name) + "ViewSpecificationsTest";
        var code = new JavaCodeBuilder(package).Block($"class {name}", body =>
        {
            foreach (var spec in specs)
            {
                var method = JavaNames.Member(spec.Name);
                body.Line("@org.junit.jupiter.api.Test")
                    .Line($"@org.junit.jupiter.api.Disabled({JavaCodeBuilder.StringLiteral(AxonRefusalCodes.Reduction + ": reducer implementation is a throwing extension point")})")
                    .Block($"void {method}()", test => test
                        .UnsupportedIntent("execute the read model specification using its reducer", spec.Id.ToString(), AxonRefusalCodes.Reduction)
                        .Line("throw new UnsupportedOperationException(\"Reducer implementation required\");"));
                context.Degradation.Unsupported(AxonRefusalCodes.Reduction, spec.Id, "Reducer-dependent specifications remain disabled until its generated interface is implemented.");
                AxonSpecificationsEmitter.Register(context, spec.Id, package + "." + name, method, AxonRefusalCodes.Reduction);
            }
        });
        context.AddJava(package, name, code.Build(), true, [.. specs.Select(spec => spec.Id)]);
    }
}
