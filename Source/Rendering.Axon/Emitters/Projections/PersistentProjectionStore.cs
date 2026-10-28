// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Runtime;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal static class PersistentProjectionStore
{
    internal static void Emit(AxonEmissionContext context, ViewJavaTypes types, string package, SemanticReadModel model)
    {
        var name = JavaNames.Type(model.Name);
        var code = new JavaCodeBuilder(package).Declare(name).Declare(name + "StoreConfiguration");
        var store = code.Reference(AxonRuntimePackage.Type(types.BasePackage, "ReadModelStore"));
        var memory = code.Reference(AxonRuntimePackage.Type(types.BasePackage, "InMemoryReadModelStore"));
        var persistent = code.Reference(AxonRuntimePackage.Type(types.BasePackage, "views.JpaReadModelStore"));
        var bootstrap = code.Reference(types.BasePackage + ".persistence.PersistentViewConfiguration");
        var qualifier = JavaCodeBuilder.StringLiteral(package + "." + name);
        code.DiffersFromScreenplay("Default-profile read-model rows are volatile. The persistent profile stores this read model durably (STAGE-AXON-0308).")
            .Line($"@{code.Reference("org.springframework.context.annotation.Configuration")}({JavaCodeBuilder.StringLiteral(package + "." + name + "StoreConfiguration")})")
            .Line($"@{code.Reference("org.springframework.context.annotation.PropertySource")}(\"classpath:axon-views.properties\")")
            .Block($"public class {name}StoreConfiguration", body => body
                .Line($"@{body.Reference("org.springframework.context.annotation.Profile")}(\"!persistent\")")
                .Line($"@{body.Reference("org.springframework.context.annotation.Bean")}({qualifier})")
                .Block($"public {store}<{body.Reference("java.lang.Object")}, {name}> store()", method => method.Line($"return new {memory}<>();"))
                .Line($"@{body.Reference("org.springframework.context.annotation.Profile")}(\"persistent\")")
                .Line($"@{body.Reference("org.springframework.context.annotation.Bean")}({qualifier})")
                .Block($"public {store}<{body.Reference("java.lang.Object")}, {name}> persistentStore({bootstrap} generation, {body.Reference("jakarta.persistence.EntityManagerFactory")} factory, {body.Reference("tools.jackson.databind.json.JsonMapper")} mapper)", method =>
                    method.Line($"return new {persistent}<>(factory, mapper, {qualifier}, {name}.class);")));
        ViewJavaTypes.Add(context, package, name + "StoreConfiguration", code.Build(), model.Id);
    }
}
