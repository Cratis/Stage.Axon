// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters.Runtime;

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
        context.Artifacts.Add(PlannedArtifact.CreateText("pom.xml", AxonMavenProject.Pom(context.BasePackage, context.ArtifactId, bearer, runtimeVersion)));
        AddAsset(context, "mvnw", "mvnw");
        AddAsset(context, "mvnw.cmd", "mvnw.cmd");
        AddAsset(context, "maven-wrapper.properties", ".mvn/wrapper/maven-wrapper.properties");
        context.AddJava(context.BasePackage, "Application", AxonApplicationScaffold.Application(context.BasePackage));
        context.AddJava(context.BasePackage + ".infrastructure", "AxonConfiguration", AxonApplicationScaffold.Infrastructure(context.BasePackage));
        context.Artifacts.Add(PlannedArtifact.CreateText(".gitignore", "target/\ndata/\n.idea/\n*.iml\n"));
        context.Artifacts.Add(PlannedArtifact.CreateText(
            "src/test/resources/spring.properties",
            "# Axon entity registration cannot be repeated when Spring restarts a paused test context.\nspring.test.context.cache.pause=NEVER\n"));
        context.Artifacts.Add(PlannedArtifact.CreateText("src/main/resources/application.yml", "server:\n  address: 127.0.0.1\nspring:\n  application:\n    name: " + context.BasePackage + (bearer ? "\n  security:\n    oauth2:\n      resourceserver:\n        jwt:\n          authorities-claim-name: roles\n          authority-prefix: ROLE_" : string.Empty) + "\naxon:\n  axonserver:\n    enabled: false\n"));
        context.Artifacts.Add(PlannedArtifact.CreateText("README.md", "# Generated Axon application\n\nRequires JDK 25 or later.\n\n```sh\nchmod +x mvnw\n./mvnw verify\n./mvnw spring-boot:run\n```\n\nOn Windows use `mvnw.cmd`. Maven Wrapper 3.3.4 downloads Maven 3.9.16 on first use. The wrapper scripts retain their Apache-2.0 license headers.\n\nThe default event store, tokens and read-model stores are in memory. Data is lost on restart; do not use this default for production. The service binds to loopback by default. " + (bearer ? "Protected endpoints accept bearer JWTs in the `Authorization` header. Set `spring.security.oauth2.resourceserver.jwt.issuer-uri` (or `jwk-set-uri` together with `issuer-uri`, or `public-key-location`) and `spring.security.oauth2.resourceserver.jwt.audiences`; until a provider is configured every token is rejected. A configured decoder source requires non-empty `spring.security.oauth2.resourceserver.jwt.audiences`, otherwise startup is refused. Roles are read from the `roles` claim (`authorities-claim-name`, `authority-prefix`)." : "Supply an identity provider before exposing protected endpoints.") + " No users or credentials are generated.\n\nFor a local demo only, run `./mvnw spring-boot:run -Dspring-boot.run.profiles=demo` (or set `SPRING_PROFILES_ACTIVE=demo`). The opt-in `demo` profile authenticates every HTTP request as `demo` with all modeled roles, ignores Basic and bearer credentials, and logs a startup WARN. It is off by default. Never activate it in production.\n\nThis backend does not use Axon Server or containers. An optional application-owned `axonserver` profile may enable Axon Server; it is not generated or exercised in CI. Unsupported Screenplay constructs produce diagnostics and explicit intent comments; inspect these before deployment.\n"));
    }

    static void AddAsset(AxonEmissionContext context, string resource, string path)
    {
        using var stream = typeof(AxonScaffoldEmitter).Assembly.GetManifestResourceStream("Cratis.Stage.Rendering.Axon.Emitters.Scaffold.Assets." + resource)!;
        using var reader = new StreamReader(stream);
        context.Artifacts.Add(PlannedArtifact.CreateText(path, reader.ReadToEnd()));
    }
}
