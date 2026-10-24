// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Xml.Linq;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;

namespace Cratis.Stage.Rendering.Axon.Verification;

internal static class CorpusRenderer
{
    internal static async Task Render(string repository, string output)
    {
        if (Directory.Exists(output) || File.Exists(output))
        {
            throw new NativeVerificationFailed("The generated corpus destination must not already exist.");
        }

        var applications = CorpusApplications.Find(repository);
        Directory.CreateDirectory(output);
        foreach (var (name, root) in applications)
        {
            await RenderApplication(name, root, Path.Combine(output, name));
        }

        var modules = applications.Select(application => application.Name).ToArray();
        await File.WriteAllTextAsync(Path.Combine(output, "applications.json"), JsonSerializer.Serialize(modules));
        await WriteReactor(output, modules);
    }

    static async Task RenderApplication(string name, string root, string destination)
    {
        var applicationName = "Corpus" + string.Concat(name.Split('-').Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
        var compilation = File.Exists(root)
            ? await SemanticModelLoader.LoadAsync(Path.GetDirectoryName(root)!, [Path.GetFileName(root)], null, applicationName)
            : await SemanticModelLoader.LoadAsync(root, [], null, applicationName);
        if (!compilation.Success)
        {
            throw new NativeVerificationFailed($"{name}: compilation failed: {string.Join("; ", compilation.Diagnostics.Select(diagnostic => diagnostic.Code + ": " + diagnostic.Message))}");
        }

        var loaded = compilation.Loaded!;
        var plan = AxonRendering.PlanFrom(loaded, new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id));
        if (!plan.Success || !plan.Refusals.IsEmpty)
        {
            throw new NativeVerificationFailed($"{name}: rendering failed: {string.Join("; ", plan.Diagnostics.Select(diagnostic => diagnostic.Code + ": " + diagnostic.Message))}");
        }

        await WriteArtifacts(destination, plan);
        Console.WriteLine($"{name}: rendered {plan.Artifacts.Length} artifacts; {plan.Diagnostics.Length} diagnostics; no refusals");
    }

    static async Task WriteArtifacts(string destination, ArtifactRenderPlan plan)
    {
        foreach (var artifact in plan.Artifacts)
        {
            var path = Path.GetFullPath(Path.Combine(destination, artifact.RelativePath));
            if (!path.StartsWith(Path.GetFullPath(destination) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new NativeVerificationFailed("An artifact escaped its application directory.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, [.. artifact.Bytes]);
        }
    }

    static async Task WriteReactor(string output, string[] modules)
    {
        XNamespace ns = "http://maven.apache.org/POM/4.0.0";
        var reactor = new XDocument(new XElement(ns + "project", new XElement(ns + "modelVersion", "4.0.0"), new XElement(ns + "groupId", "org.cratis.stage.verification"), new XElement(ns + "artifactId", "axon-corpus"), new XElement(ns + "version", "1.0.0-SNAPSHOT"), new XElement(ns + "packaging", "pom"), new XElement(ns + "modules", modules.Select(module => new XElement(ns + "module", module)))));
        await File.WriteAllTextAsync(Path.Combine(output, "pom.xml"), reactor + "\n");
        foreach (var relative in new[] { "mvnw", "mvnw.cmd", ".mvn/wrapper/maven-wrapper.properties" })
        {
            var target = Path.Combine(output, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(output, modules[0], relative), target);
        }
    }
}
