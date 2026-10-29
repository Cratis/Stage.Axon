// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters.Runtime;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Scaffold;

internal sealed class AxonScaffoldEmitter : IAxonEmitter
{
    public bool IsImplemented => true;

    public void Emit(AxonEmissionContext context) => Emit(context, AxonRuntimePackage.Version);

    internal static void Emit(AxonEmissionContext context, string runtimeVersion)
    {
        if (AxonMavenProject.IsLocalVersion(runtimeVersion))
        {
            context.Degradation.Warn(AxonRefusalCodes.LocalRuntime, context.Request.Model.Application.Id, AxonMavenProject.LocalBuildWarning(runtimeVersion));
        }

        var bearer = HttpAuthenticationInput.Bearer(context.Request.Profile);
        var persistent = PersistentProfile.Offered(context);
        context.Artifacts.Add(PlannedArtifact.CreateText("pom.xml", AxonMavenProject.Pom(context.BasePackage, context.ArtifactId, bearer, runtimeVersion)));
        AddAsset(context, "mvnw", "mvnw");
        AddAsset(context, "mvnw.cmd", "mvnw.cmd");
        AddAsset(context, "maven-wrapper.properties", ".mvn/wrapper/maven-wrapper.properties");
        context.AddJava(context.BasePackage, "Application", AxonApplicationScaffold.Application(context.BasePackage, UsesScheduling(context)));
        context.AddJava(context.BasePackage + ".infrastructure", "AxonConfiguration", AxonApplicationScaffold.Infrastructure(context.BasePackage, persistent));
        context.Artifacts.Add(PlannedArtifact.CreateText(".gitignore", "target/\ndata/\n.idea/\n*.iml\n"));
        context.Artifacts.Add(PlannedArtifact.CreateText(
            "src/test/resources/spring.properties",
            "# Axon entity registration cannot be repeated when Spring restarts a paused test context.\nspring.test.context.cache.pause=NEVER\n"));
        context.Artifacts.Add(PlannedArtifact.CreateText("src/main/resources/application.yml", "server:\n  address: 127.0.0.1\nspring:\n  application:\n    name: " + context.BasePackage + (bearer ? "\n  security:\n    oauth2:\n      resourceserver:\n        jwt:\n          authorities-claim-name: roles\n          authority-prefix: ROLE_" : string.Empty) + "\naxon:\n  axonserver:\n    enabled: false\n"));
        context.Artifacts.Add(PlannedArtifact.CreateText("README.md", AxonApplicationReadme.Text(bearer, persistent)));
        if (persistent) EmitPersistentProfile(context);
        else ExplainMissingPersistentProfile(context);
    }

    static void EmitPersistentProfile(AxonEmissionContext context)
    {
        context.Artifacts.Add(PlannedArtifact.CreateText(
            $"src/main/resources/application-{PersistentProfile.Name}.yml",
            "# Optional durable storage: ./mvnw spring-boot:run -Dspring-boot.run.profiles=persistent\n" +
            "# Events: AggregateBasedJpaEventStorageEngine; tokens: JpaTokenStore; read models: JPA. One H2 file database under data/.\n" +
            "# Set spring.datasource.* (for example SPRING_DATASOURCE_URL) and add the driver to use another JDBC database.\n" +
            "spring:\n  datasource:\n    url: jdbc:h2:file:./data/application\n    username: sa\n    password: \"\"\n  jpa:\n    hibernate:\n      ddl-auto: update\n"));
        var package = context.BasePackage + ".infrastructure";
        var code = new JavaCodeBuilder(package).Declare("PersistentProfileTest")
            .Import("static org.junit.jupiter.api.Assertions.assertInstanceOf")
            .Import("org.axonframework.common.configuration.Configuration")
            .Import("org.axonframework.eventsourcing.eventstore.EventStorageEngine")
            .Import("org.axonframework.eventsourcing.eventstore.jpa.AggregateBasedJpaEventStorageEngine")
            .Import("org.axonframework.messaging.eventhandling.processing.streaming.token.store.TokenStore")
            .Import("org.axonframework.messaging.eventhandling.processing.streaming.token.store.jpa.JpaTokenStore")
            .Import("org.junit.jupiter.api.Test")
            .Import("org.springframework.beans.factory.annotation.Autowired")
            .Import("org.springframework.boot.test.context.SpringBootTest")
            .Import("org.springframework.context.ApplicationContext")
            .Import("org.springframework.test.context.ActiveProfiles")
            .Line("@SpringBootTest(properties = \"spring.datasource.url=jdbc:h2:file:./target/persistent-profile/${random.uuid}\")")
            .Line($"@ActiveProfiles(\"{PersistentProfile.Name}\")")
            .Block("class PersistentProfileTest", body => body
                .Line("@Autowired")
                .Line("private Configuration configuration;")
                .Line("@Autowired")
                .Line("private ApplicationContext context;")
                .Line("@Test")
                .Block("void stores_events_in_the_aggregate_based_jpa_engine()", method => method.Line("assertInstanceOf(AggregateBasedJpaEventStorageEngine.class, configuration.getComponent(EventStorageEngine.class));"))
                .Line("@Test")
                .Block("void stores_tokens_in_jpa()", method => method.Line("assertInstanceOf(JpaTokenStore.class, context.getBean(TokenStore.class));")))
            .Build();
        context.AddJava(package, "PersistentProfileTest", code, test: true);
    }

    static void ExplainMissingPersistentProfile(AxonEmissionContext context)
    {
        const string reason = "The optional persistent profile is not generated: modeled constraints need multi-tag (DCB) event consistency, which AggregateBasedJpaEventStorageEngine does not support. Events stay in memory.";
        context.Degradation.Warn(AxonRefusalCodes.PersistentProfile, context.Request.Model.Application.Id, reason);
        context.Degradation.Note($"{AxonRefusalCodes.PersistentProfile}: {reason}");
    }

    static bool UsesScheduling(AxonEmissionContext context) => ScopeSelection.Locate(context.Request.Model.Application)
        .SelectMany(located => located.Slice.Reactions).SelectMany(reaction => reaction.Triggers)
        .Any(trigger => trigger.Kind is SemanticReactionTriggerKind.Interval or SemanticReactionTriggerKind.Schedule);

    static void AddAsset(AxonEmissionContext context, string resource, string path)
    {
        using var stream = typeof(AxonScaffoldEmitter).Assembly.GetManifestResourceStream("Cratis.Stage.Rendering.Axon.Emitters.Scaffold.Assets." + resource)!;
        using var reader = new StreamReader(stream);
        context.Artifacts.Add(PlannedArtifact.CreateText(path, reader.ReadToEnd()));
    }
}
