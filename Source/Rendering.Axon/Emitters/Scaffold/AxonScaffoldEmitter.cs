// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Scaffold;

internal sealed class AxonScaffoldEmitter : IAxonEmitter
{
    public bool IsImplemented => true;

    public void Emit(AxonEmissionContext context)
    {
        context.Artifacts.Add(PlannedArtifact.CreateText("pom.xml", AxonMavenProject.Pom(context.BasePackage)));
        AddAsset(context, "mvnw", "mvnw");
        AddAsset(context, "mvnw.cmd", "mvnw.cmd");
        AddAsset(context, "maven-wrapper.properties", ".mvn/wrapper/maven-wrapper.properties");
        context.AddJava(context.BasePackage, "Application", AxonApplicationScaffold.Application(context.BasePackage));
        context.AddJava(context.BasePackage + ".infrastructure", "AxonConfiguration", AxonApplicationScaffold.Infrastructure(context.BasePackage));
        context.Artifacts.Add(PlannedArtifact.CreateText(".gitignore", "target/\ndata/\n.idea/\n*.iml\n"));
        context.Artifacts.Add(PlannedArtifact.CreateText("src/main/resources/application-persistent.properties", AxonPersistentScaffold.Properties));
        var processors = context.Request.Model.Application.Modules.Select(module => "      " + JavaNames.PackageSegment(module.Name) + ":\n        mode: subscribing");
        context.Artifacts.Add(PlannedArtifact.CreateText("src/main/resources/application.yml", "# Copyright (c) Cratis. All rights reserved. Licensed under the MIT license.\nserver:\n  address: 127.0.0.1\nspring:\n  application:\n    name: " + context.BasePackage + "\naxon:\n  axonserver:\n    enabled: false\n  eventhandling:\n    processors:\n" + string.Join('\n', processors) + "\n"));
        context.Artifacts.Add(PlannedArtifact.CreateText("README.md", "# Generated Axon application\n\nRequires JDK 21 or later.\n\n```sh\nchmod +x mvnw\n./mvnw verify\n./mvnw spring-boot:run\n```\n\nOn Windows use `mvnw.cmd`. Maven Wrapper 3.3.4 downloads Maven 3.9.16 on first use. The wrapper scripts retain their Apache-2.0 license headers.\n\nThe default event store, tokens and read-model stores are in memory. Data is lost on restart; do not use this default for production. The service binds to loopback by default. Supply an identity provider before exposing protected endpoints. No users or credentials are generated.\n\nThis backend does not use Axon Server or containers. An optional application-owned `axonserver` profile may enable Axon Server; it is not generated or exercised in CI. Unsupported Screenplay constructs produce diagnostics and explicit intent comments; inspect these before deployment.\n"));
    }

    static void AddAsset(AxonEmissionContext context, string resource, string path)
    {
        using var stream = typeof(AxonScaffoldEmitter).Assembly.GetManifestResourceStream("Cratis.Stage.Rendering.Axon.Emitters.Scaffold.Assets." + resource)!;
        using var reader = new StreamReader(stream);
        context.Artifacts.Add(PlannedArtifact.CreateText(path, reader.ReadToEnd()));
    }
}
