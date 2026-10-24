// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Cratis.Stage.Contracts.Semantics;

namespace Cratis.Stage.Rendering.Axon.for_AxonArtifactRenderPlanner.given;

public static class blueprint_models
{
    static readonly Dictionary<string, Task<LoadedSemanticModel>> _models = new(StringComparer.Ordinal);

    public static Task<LoadedSemanticModel> Load(string sample)
    {
        lock (_models)
        {
            if (!_models.TryGetValue(sample, out var model))
            {
                model = SemanticModelLoader.LoadFromPathAsync(Path.Combine(Root(), "Verification", "live-blueprints", "models", sample), null, "Sample");
                _models.Add(sample, model);
            }

            return model;
        }
    }

    internal static string Root([CallerFilePath] string path = "")
    {
        var directory = Directory.GetParent(path);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Stage.slnx")))
        {
            directory = directory.Parent;
        }

        return directory.FullName;
    }
}
