// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.CanonicalCorpus;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Cratis.Stage.Rendering.Axon.for_AxonArtifactRenderPlanner.given;

namespace Cratis.Stage.Rendering.Axon.for_AxonRendering.given;

public class a_screen_composition_model : Specification
{
    protected LoadedSemanticModel loaded;
    string _directory;

    async Task Establish()
    {
        _directory = Path.Combine(blueprint_models.Root(), ".ai-work/axon5", "screen-corpus-" + Guid.NewGuid().ToString("N"));
        foreach (var document in ScreenCompositionCorpus.V1.SourceForms.Single(form => form.Name == "folder").Documents)
        {
            var path = Path.Combine(_directory, document.DisplayPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, document.Bytes.AsSpan());
        }
        loaded = await SemanticModelLoader.LoadFromPathAsync(_directory, null, ScreenCompositionCorpus.V1.ApplicationName);
    }

    protected AxonRenderRequest Request(bool strict) => new(loaded.Model, loaded.Plan, new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id))
    {
        ImplementationRequirements = loaded.ImplementationRequirements,
        ImplementationContents = loaded.ImplementationContents,
        TypedContextDescriptors = loaded.TypedContextDescriptors,
        AttachmentDiagnostics = loaded.AttachmentDiagnostics,
        ReleaseBaseline = loaded.ReleaseBaseline,
        Options = new() { Strict = strict }
    };

    void Destroy()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
