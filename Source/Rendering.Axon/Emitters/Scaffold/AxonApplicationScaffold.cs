// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Scaffold;

internal static class AxonApplicationScaffold
{
    internal static string Application(string package) => new JavaCodeBuilder(package)
        .Import("org.springframework.boot.SpringApplication")
        .Import("org.springframework.boot.autoconfigure.SpringBootApplication")
        .Line("@SpringBootApplication")
        .Block("public class Application", body => body.Block("public static void main(String[] args)", method => method.Line("SpringApplication.run(Application.class, args);")))
        .Build();

    internal static string Infrastructure(string package) => new JavaCodeBuilder(package + ".infrastructure")
        .Import("java.time.Clock")
        .Import("org.springframework.context.annotation.Bean")
        .Import("org.springframework.context.annotation.Configuration")
        .Import("org.axonframework.eventsourcing.eventstore.EventStorageEngine")
        .Import("org.axonframework.eventsourcing.eventstore.inmemory.InMemoryEventStorageEngine")
        .Import("org.axonframework.messaging.eventhandling.processing.streaming.token.store.TokenStore")
        .Import("org.axonframework.messaging.eventhandling.processing.streaming.token.store.inmemory.InMemoryTokenStore")
        .Import("tools.jackson.databind.JacksonModule")
        .Import(package + ".runtime.json.ConceptsModule")
        .Import(package + ".runtime.UniqueValueStore")
        .Import(package + ".runtime.InMemoryUniqueValueStore")
        .Line("@Configuration")
        .Block("public class AxonConfiguration", body => body
            .Line("@Bean")
            .Line("EventStorageEngine eventStorageEngine() { return new InMemoryEventStorageEngine(); }")
            .Line("@Bean")
            .Line("TokenStore tokenStore() { return new InMemoryTokenStore(); }")
            .Line("@Bean")
            .Line("Clock clock() { return Clock.systemUTC(); }")
            .Line("@Bean")
            .Line("JacksonModule conceptsModule() { return new ConceptsModule(); }")
            .Line("@Bean")
            .Line("UniqueValueStore uniqueValueStore() { return new InMemoryUniqueValueStore(); }"))
        .Build();
}
