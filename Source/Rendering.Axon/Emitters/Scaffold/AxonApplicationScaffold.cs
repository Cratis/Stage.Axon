// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Scaffold;

internal static class AxonApplicationScaffold
{
    internal static string Application(string package, bool scheduling = false)
    {
        var code = new JavaCodeBuilder(package).Declare("Application")
            .Import("org.springframework.boot.SpringApplication")
            .Import("org.springframework.boot.autoconfigure.SpringBootApplication");
        if (scheduling) code.Import("org.springframework.scheduling.annotation.EnableScheduling");
        code.Line("@SpringBootApplication");
        if (scheduling) code.Line("@EnableScheduling");
        return code.Block("public class Application", body => body.Block("public static void main(String[] args)", method => method.Line("SpringApplication.run(Application.class, args);")))
            .Build();
    }

    internal static string Infrastructure(string package, bool persistent)
    {
        var code = new JavaCodeBuilder(package + ".infrastructure").Declare("AxonConfiguration")
            .Import("java.time.Clock")
            .Import("org.springframework.context.annotation.Bean")
            .Import("org.springframework.context.annotation.Configuration")
            .Import("org.axonframework.eventsourcing.eventstore.EventStorageEngine")
            .Import("org.axonframework.eventsourcing.eventstore.inmemory.InMemoryEventStorageEngine")
            .Import("org.axonframework.messaging.eventhandling.processing.streaming.token.store.TokenStore")
            .Import("org.axonframework.messaging.eventhandling.processing.streaming.token.store.inmemory.InMemoryTokenStore")
            .Import("tools.jackson.databind.JacksonModule")
            .Import("io.cratis.fundamentals.jackson.ConceptsModule")
            .Import("io.cratis.fundamentals.jackson.ConceptKeyFormat")
            .Import("io.cratis.fundamentals.jackson.TemporalModule")
            .Import("io.cratis.fundamentals.jackson.TimeSpanModule");
        if (persistent) code.Import("org.springframework.context.annotation.Profile");
        return code
            .Line("@Configuration")
            .Block("public class AxonConfiguration", body =>
            {
                if (!persistent) body.Line($"// No persistent profile: modeled constraints need multi-tag event consistency ({AxonRefusalCodes.PersistentProfile}, see UNSUPPORTED.md).");
                StoreBean(body, "EventStorageEngine eventStorageEngine() { return new InMemoryEventStorageEngine(); }", persistent);
                StoreBean(body, "TokenStore tokenStore() { return new InMemoryTokenStore(); }", persistent);
                body.Line("@Bean")
                    .Line("Clock clock() { return Clock.systemUTC(); }")
                    .Line("@Bean")
                    .Line("JacksonModule conceptsModule() { return new ConceptsModule(ConceptKeyFormat.EMBEDDED_JSON); }")
                    .Line("@Bean")
                    .Line("JacksonModule temporalModule() { return new TemporalModule(); }")
                    .Line("@Bean")
                    .Line("JacksonModule timeSpanModule() { return new TimeSpanModule(); }");
            })
            .Build();
    }

    static void StoreBean(JavaCodeBuilder body, string declaration, bool persistent)
    {
        body.Line("@Bean");
        if (persistent) body.Line($"@Profile(\"!{PersistentProfile.Name}\")");
        body.Line(declaration);
    }
}
