// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.for_AxonArtifactRenderPlanner.given;
using Cratis.Stage.Rendering.Axon.Verification.for_CorpusApplications.given;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Verification.for_EmitterSafetyNet;

public class when_a_failed_type_has_a_dependent
{
    [Fact]
    public async Task should_compile_the_stub_and_its_dependent_module()
    {
        var root = the_repository.Root();
        var loaded = await blueprint_models.Load("orders");
        var request = new ArtifactRenderRequest(loaded.Model, loaded.Plan, AxonRendering.CreateProfile(), new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id));
        var plan = a_failed_emitter.Plan(request);
        Assert.True(plan.Success);
        var directory = Path.Combine(root, ".ai-work", "axon-s1", "javac", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var start = new ProcessStartInfo("javac") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("-Werror");
        start.ArgumentList.Add("-d");
        start.ArgumentList.Add(Path.Combine(directory, "classes"));
        foreach (var artifact in plan.Artifacts.Where(artifact => artifact.RelativePath.EndsWith(".java", StringComparison.Ordinal)))
        {
            var path = Path.Combine(directory, artifact.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, [.. artifact.Bytes]);
            start.ArgumentList.Add(path);
        }
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        }
        catch (TimeoutException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }
        Assert.True(process.ExitCode == 0, await output + await errors);
        Assert.True(File.Exists(Path.Combine(directory, "classes", "proof", "Dependent.class")));
        Assert.True(File.Exists(Path.Combine(directory, "classes", "module-info.class")));
    }
}
