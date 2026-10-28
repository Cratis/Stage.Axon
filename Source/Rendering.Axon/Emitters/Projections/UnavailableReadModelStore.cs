// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters.Runtime;
using Cratis.Stage.Rendering.Axon.Emitters.Specifications;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal static class UnavailableReadModelStore
{
    internal const string Message = "STAGE-AXON-0306: Read model unavailable.";
    static readonly string[] _operations = ["all()", "get(\"retained\")", "put(\"retained\", null)", "remove(\"retained\")", "clear()", "update(\"retained\", current -> { throw new AssertionError(\"Callback must not see rows\"); })"];

    internal static void Emit(AxonEmissionContext context, string package, SemanticReadModel model, string reason)
    {
        var name = JavaNames.Type(model.Name);
        var configuration = name + "StoreConfiguration";
        var code = new JavaCodeBuilder(package).Declare(name).Declare(configuration);
        var store = code.Reference(AxonRuntimePackage.Type(context, "ReadModelStore"));
        var key = code.Reference("java.lang.Object");
        var exception = code.Reference("java.lang.IllegalStateException");
        code.UnsupportedIntent("quarantine the entire read model: " + reason + " No projector is registered and no stored row is readable in any profile.", model.Id.ToString(), AxonRefusalCodes.Reduction)
            .Line($"@{code.Reference("org.springframework.context.annotation.Configuration")}({JavaCodeBuilder.StringLiteral(package + "." + configuration)})")
            .Block($"public class {configuration}", body => body
                .Line($"@{body.Reference("org.springframework.context.annotation.Bean")}({JavaCodeBuilder.StringLiteral(package + "." + name)})")
                .Block($"public {store}<{key}, {name}> store()", method => method
                    .Line($"return new {store}<>() {{")
                    .Line($"    public {method.Reference("java.util.Optional")}<{name}> get({key} key) {{ throw new {exception}({JavaCodeBuilder.StringLiteral(Message)}); }}")
                    .Line($"    public {method.Reference("java.util.List")}<{name}> all() {{ throw new {exception}({JavaCodeBuilder.StringLiteral(Message)}); }}")
                    .Line($"    public void put({key} key, {name} value) {{ throw new {exception}({JavaCodeBuilder.StringLiteral(Message)}); }}")
                    .Line($"    public void remove({key} key) {{ throw new {exception}({JavaCodeBuilder.StringLiteral(Message)}); }}")
                    .Line($"    public void clear() {{ throw new {exception}({JavaCodeBuilder.StringLiteral(Message)}); }}")
                    .Line($"    public {name} update({key} key, {method.Reference("java.util.function.UnaryOperator")}<{name}> update) {{ throw new {exception}({JavaCodeBuilder.StringLiteral(Message)}); }}")
                    .Line("};")));
        context.AddJava(package, configuration, code.Build(), sources: model.Id);
    }

    internal static void EmitSpecifications(AxonEmissionContext context, ViewJavaTypes types, LocatedSemanticSlice owner, SemanticReadModel model, SemanticSpecification[] specs)
    {
        var package = types.Package(owner);
        var name = JavaNames.Type(model.Name) + "QuarantineSpecificationsTest";
        var code = new JavaCodeBuilder(package).Declare(name).Declare(JavaNames.Type(model.Name) + "StoreConfiguration");
        code.Block("class " + name, body =>
        {
            foreach (var spec in specs)
            {
                var method = JavaNames.Member(spec.Name) + "RefusesUnavailableModel";
                body.Line("@" + body.Reference("org.junit.jupiter.api.Test"))
                    .Line($"@{body.Reference("org.junit.jupiter.api.DisplayName")}({JavaCodeBuilder.StringLiteral(spec.Id + ": quarantined; modeled reduction is unavailable")})")
                    .Block("void " + method + "()", test =>
                    {
                        test.UnsupportedIntent("refuse access instead of asserting successful reduction", spec.Id.ToString(), AxonRefusalCodes.Reduction)
                            .Line($"var store = new {JavaNames.Type(model.Name)}StoreConfiguration().store();");
                        var assertions = test.Reference("org.junit.jupiter.api.Assertions");
                        var exception = test.Reference("java.lang.IllegalStateException");
                        foreach (var operation in _operations)
                            test.Line($"{assertions}.assertThrows({exception}.class, () -> store.{operation});");
                    });
                AxonSpecificationsEmitter.Register(context, spec.Id, package + "." + name, method, null);
            }
        });
        context.AddJava(package, name, code.Build(), true, [.. specs.Select(spec => spec.Id)]);
    }
}
